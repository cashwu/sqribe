DECLARE @Catalog NVARCHAR(128),
		@SQL NVARCHAR(MAX),
		@SQL2 NVARCHAR(MAX),
		@COLS NVARCHAR(4000),
		@Owner NVARCHAR(128),
		@Table NVARCHAR(128),
		@ObjectID INT,
		@AccentOn BIT,
		@CatalogID INT,
		@IndexID INT,
		@Max_objectId INT,
		@NL CHAR(2),
		@i int

SET @i = 1;

-- Cursor to fetch the name of catalogs one by one for the current database

declare FTCur cursor for SELECT Name
FROM sys.fulltext_catalogs
	ORDER BY NAME

OPEN FTCur
 

FETCH FTCur INTO @Catalog

WHILE @@FETCH_status >= 0

BEGIN

SET @i = @i + 1;

SELECT
	@NL = CHAR(13) + CHAR(10)

-- Check catalog exists
IF EXISTS
(
	SELECT Name
	FROM sys.fulltext_catalogs
	WHERE Name = @Catalog
) BEGIN
		-- Store the catalog details
		SELECT
			@CatalogID = i.fulltext_catalog_id
			,@ObjectID = 0
			,@Max_objectId = MAX(object_id)
			,@AccentOn = is_accent_sensitivity_on
		FROM sys.fulltext_index_catalog_usages AS i
		JOIN sys.fulltext_catalogs c
			ON i.fulltext_catalog_id = c.fulltext_catalog_id
		WHERE c.Name = @Catalog
		GROUP BY	i.fulltext_catalog_id
					,is_accent_sensitivity_on

		-- Script out catalog
		SET @SQL2 = 'CREATE FULLTEXT CATALOG ' + QUOTENAME(@Catalog) + @NL
		SET @SQL2 = @SQL2 + 'WITH ACCENT_SENSITIVITY = ' + CASE @AccentOn
			WHEN 1
				THEN 'ON'
			ELSE 'OFF'
		END
		SET @SQL2 = @SQL2 + @NL + 'GO' + @NL

		SELECT @SQL2

		END

		DECLARE FTObject CURSOR FOR SELECT	MIN(i.object_id) objectId
									,u.name AS schemaName
									,t.Name
									,unique_index_id
									,c.name as catalogueName
		FROM sys.tables AS t
		JOIN sys.schemas AS u
			ON u.schema_id = t.schema_id
		JOIN sys.fulltext_indexes i
			ON t.object_id = i.object_id
		JOIN sys.fulltext_catalogs c
			ON i.fulltext_catalog_id = c.fulltext_catalog_id
		WHERE 1 = 1 
		AND c.Name = @Catalog
		--AND i.object_id > @ObjectID
		GROUP BY	u.name
					,t.Name
					,unique_index_id
					,c.name

		OPEN FTObject

		FETCH FTObject INTO @ObjectID, @Owner, @Table, @IndexID, @Catalog
		-- Loop through all fulltext indexes within catalog

				WHILE @@FETCH_status >= 0 
				BEGIN
		
					-- Script Fulltext Index
					SELECT
						@COLS = NULL
						,@SQL = 'CREATE FULLTEXT INDEX ON ' + QUOTENAME(@Owner) + '.' + QUOTENAME(@Table) + ' (' + @NL
					-- Script columns in index
					SELECT
						@COLS = CASE type_name(c.system_type_id)
							WHEN 'image'
								THEN COALESCE(@COLS + ',', '') + QUOTENAME(c.Name) + ' TYPE COLUMN [' + tc.name + ']' + @NL
							WHEN 'varbinary'
								THEN COALESCE(@COLS + ',', '') + QUOTENAME(c.Name) + ' TYPE COLUMN [' + tc.name + ']' + @NL
							ELSE COALESCE(@COLS + ',', '') + QUOTENAME(c.Name) + ' Language ' + CAST(Language_id AS varchar) + ' ' + @NL
						END
					FROM sys.fulltext_index_columns AS fi
					JOIN sys.columns AS c
						ON c.object_id = fi.object_id
						AND c.column_id = fi.column_id
					LEFT OUTER JOIN sys.columns AS tc
						ON tc.column_id = fi.type_column_id AND tc.object_id = fi.object_id
					WHERE fi.object_id = @ObjectID

					-- Script unique key index
					SELECT
						@SQL = @SQL + @COLS + ') ' + @NL + 'KEY INDEX ' + QUOTENAME(i.Name) + @NL +
						'ON ' + QUOTENAME(@Catalog) + @NL +
						'WITH CHANGE_TRACKING ' + fi.change_tracking_state_desc + @NL + 'GO' + @NL
					FROM sys.indexes AS i
					JOIN sys.fulltext_indexes AS fi
						ON i.object_id = fi.object_id
					WHERE i.Object_ID = @ObjectID
					AND Index_Id = @IndexID

					-- Output script SQL
				SELECT @SQL

					FETCH FTObject INTO @ObjectID, @Owner, @Table, @IndexID, @Catalog
				END
		CLOSE FTObject;
		DEALLOCATE FTObject;
FETCH FTCur INTO @catalog
END
CLOSE FTCur
DEALLOCATE FTCur
