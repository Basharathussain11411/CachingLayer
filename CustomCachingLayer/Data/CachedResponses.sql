CREATE TABLE CachedResponses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CacheKey NVARCHAR(450) NOT NULL,
    Response NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NOT NULL
);

CREATE INDEX IX_CachedResponses_CacheKey ON CachedResponses(CacheKey);
CREATE INDEX IX_CachedResponses_ExpiresAt ON CachedResponses(ExpiresAt);
