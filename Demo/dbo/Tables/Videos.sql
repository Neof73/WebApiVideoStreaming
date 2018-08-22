CREATE TABLE [dbo].[Videos] (
    [Id]           UNIQUEIDENTIFIER           ROWGUIDCOL NOT NULL,
    [SerialNumber] INT                        NULL,
    [Name]         NVARCHAR (MAX)             NULL,
    [Stream]       VARBINARY (MAX) FILESTREAM NULL,
    UNIQUE NONCLUSTERED ([Id] ASC),
    UNIQUE NONCLUSTERED ([SerialNumber] ASC)
) FILESTREAM_ON [FileStreamGroup1];

