CREATE TABLE [dbo].[Streams] (
    [id]       BIGINT          IDENTITY (1, 1) NOT NULL,
    [textdata] NVARCHAR (MAX)  NULL,
    [bindata]  VARBINARY (MAX) NULL,
    CONSTRAINT [PK_Streams_1] PRIMARY KEY CLUSTERED ([id] ASC)
);

