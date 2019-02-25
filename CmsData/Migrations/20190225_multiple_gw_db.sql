IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where 
	TABLE_NAME = 'GatewaySettings' AND 
	TABLE_SCHEMA = 'dbo')
	BEGIN		
		DROP TABLE [dbo].[GatewaySettings];		
	END	
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where 
	TABLE_NAME = 'Gateways' AND 
	TABLE_SCHEMA = 'lookup')
	BEGIN				
		DROP TABLE [lookup].[Gateways]		
	END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where 
	TABLE_NAME = 'GatewayServiceType' AND 
	TABLE_SCHEMA = 'lookup')
	BEGIN		
		DROP TABLE [lookup].[GatewayServiceType]
	END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where 
	TABLE_NAME = 'Process' AND 
	TABLE_SCHEMA = 'lookup')
	BEGIN
		DROP TABLE [lookup].[Process]
	END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where 
	TABLE_NAME = 'ProcessType' AND 
	TABLE_SCHEMA = 'lookup')
	BEGIN		
		DROP TABLE [lookup].[ProcessType]
	END
GO

CREATE TABLE [lookup].[GatewayServiceType](
	[GatewayServiceTypeId][int] IDENTITY(1,1) PRIMARY KEY,
	[GatewayServiceTypeName][nvarchar](25) NOT NULL
)

INSERT INTO [lookup].[GatewayServiceType]
([GatewayServiceTypeName])
VALUES
('Redirect Link'),
('SOAP'),
('API')
GO

CREATE TABLE [lookup].[Gateways](
			[GatewayId][int] IDENTITY(1,1) PRIMARY KEY,
			[GatewayName][nvarchar](30) NOT NULL,
			[GatewayServiceTypeId][int] FOREIGN KEY REFERENCES [lookup].[GatewayServiceType]([GatewayServiceTypeId]) NOT NULL
		)

		INSERT INTO [lookup].[Gateways]
		([GatewayName],
		[GatewayServiceTypeId])
		VALUES
		('Pushpay', 1),
		('Sage', 2),
		('Transnational', 2),
		('Acceptival', 3)

CREATE TABLE [lookup].[ProcessType](
	[ProcessTypeId][int] IDENTITY(1,1) PRIMARY KEY,
	[ProcessTypeName][nvarchar](75) NOT NULL
)

INSERT INTO [lookup].[ProcessType]
([ProcessTypeName])
VALUES
('Payment'),
('No Payment Required')
GO

CREATE TABLE [lookup].[Process](
	[ProcessId][int] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[ProcessName][nvarchar](30) NOT NULL,
	[ProcessTypeId][int] FOREIGN KEY REFERENCES [lookup].[ProcessType]([ProcessTypeId]) NOT NULL
)
GO

INSERT INTO [lookup].[Process]
([ProcessName]
,[ProcessTypeId])
VALUES
('One-Time Giving', 1),
('Recurring Giving', 1),
('Online Registration', 1)
GO

CREATE TABLE [dbo].[GatewaySettings](
		[GatewaySettingId][int] IDENTITY(1,1) PRIMARY KEY,
		[GatewayId][int] FOREIGN KEY REFERENCES [lookup].[Gateways]([GatewayId]) NOT NULL,
		[ProcessId][int] UNIQUE FOREIGN KEY REFERENCES [lookup].[Process]([ProcessId]) NOT NULL,
		[Settings][nvarchar](max) NOT NULL --JSON format setting details per gateway.
		)
GO

DROP PROCEDURE IF EXISTS [dbo].[EditGatewaySettings];
GO

CREATE PROCEDURE [dbo].[EditGatewaySettings]
@GatewayId[int],
@ProcessId[int],
@Settings[nvarchar](max),
@Operation[nvarchar](50) --INSERT, DELETE, UPDATE
AS	

	IF(UPPER(RTRIM(@Operation)) = 'INSERT' OR UPPER(RTRIM(@Operation)) = 'UPDATE' OR UPPER(RTRIM(@Operation)) = 'DELETE'
	)
	BEGIN
		IF EXISTS (SELECT [ProcessTypeId] FROM [lookup].[Process] WHERE [ProcessId] = @ProcessId ) 
		BEGIN
			IF EXISTS (SELECT [GatewayId] FROM [lookup].[Gateways] WHERE [GatewayId] = @GatewayId) 
			BEGIN
				IF(UPPER(RTRIM(@Operation)) = 'INSERT')	--INSERT	
				BEGIN
				
					IF NOT EXISTS (SELECT GatewayId FROM [dbo].[GatewaySettings] WHERE [ProcessId] = @ProcessId AND [GatewayId] = @GatewayId)					
					BEGIN
						INSERT INTO [dbo].[GatewaySettings]([GatewayId],[ProcessId],[Settings])
						VALUES(@GatewayId,@ProcessId,@Settings)
						IF(@@ROWCOUNT) > 0
							SELECT CONVERT([nvarchar](8), 'Success') AS 'Status'					
						ELSE
							SELECT CONVERT([nvarchar](max), 'Error inserting data') AS 'Status'		
					END
					ELSE
					BEGIN
							SELECT CONVERT([nvarchar](max), 'Record already in DB') AS 'Status'		
					END
				END
				IF(UPPER(RTRIM(@Operation)) = 'DELETE')	--DELETE					
				BEGIN
					IF(SELECT GatewayId FROM [dbo].[GatewaySettings] WHERE [ProcessId] = @ProcessId AND [GatewayId] = @GatewayId) = 1
					BEGIN
						DELETE FROM [dbo].[GatewaySettings] WHERE [ProcessId] = @ProcessId AND [GatewayId] = @GatewayId
						SELECT CONVERT([nvarchar](max), 'Record deleted successfully') AS 'Status'						
					END
					ELSE 
					BEGIN
						SELECT CONVERT([nvarchar](max), 'No record found to be deleted') AS 'Status'		
					END
				END			
				ELSE IF(UPPER(RTRIM(@Operation)) = 'UPDATE')	--UPDATE	
				BEGIN
					IF(SELECT GatewayId FROM [dbo].[GatewaySettings] WHERE [ProcessId] = @ProcessId AND [GatewayId] = @GatewayId) = 1
					BEGIN
						UPDATE [dbo].[GatewaySettings] SET [ProcessId] = @ProcessId, [GatewayId] = @GatewayId, [Settings] = @Settings
						SELECT CONVERT([nvarchar](max), 'Record updated successfully') AS 'Status'						
					END
					ELSE
					BEGIN
						SELECT CONVERT([nvarchar](max), 'No record found to be updated') AS 'Status'
					END
				END
			END
			ELSE 
			BEGIN --NOT A VALID GATEWAY	
			SELECT CONVERT([nvarchar](max), 'Not a valid gateway.') AS 'status'
		END
		
	END
	ELSE --NOT A VALID PROCESS
	BEGIN
		SELECT CONVERT([nvarchar](max), 'Not a valid process.') AS 'Status'
	END
	END		
	ELSE
		BEGIN
			SELECT CONVERT([nvarchar](max), 'Invalid operation') AS 'Status'
		END
GO

