/* Generated from ApplicationDbContextModelSnapshot.cs. Run node generate-database-data.js after schema migrations. */
'use strict';
globalThis.DatabaseDesign = Object.freeze({
  "project": {
    "name": "Zaytouna Sourcing Portal",
    "database": "Sourcing Tender Management",
    "engine": "SQL Server",
    "schema": "dbo",
    "description": "A complete procurement workflow from department request and tender preparation through supplier bidding, committee evaluation, award, contracting, purchase orders and delivery.",
    "stores": [
      "User accounts & roles",
      "Department requests",
      "Tenders & documents",
      "Suppliers & invitations",
      "Bids & evaluations",
      "Awards & contracts",
      "Audit & workflow history"
    ],
    "hubTable": "Tenders",
    "source": "Generated from the current EF Core ApplicationDbContext model snapshot.",
    "snapshotDate": "2026-11-21",
    "excludedTables": [
      "__EFMigrationsHistory"
    ]
  },
  "groups": [
    {
      "id": "identity",
      "name": "Identity & user work"
    },
    {
      "id": "requests",
      "name": "Requests & catalog"
    },
    {
      "id": "tenders",
      "name": "Tender preparation"
    },
    {
      "id": "suppliers",
      "name": "Suppliers & invitations"
    },
    {
      "id": "bids",
      "name": "Bidding & evaluation"
    },
    {
      "id": "fulfilment",
      "name": "Contracts & fulfilment"
    },
    {
      "id": "governance",
      "name": "Governance & audit"
    }
  ],
  "tables": [
    {
      "name": "Approvals",
      "group": "tenders",
      "description": "Approvals records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ApprovalLevel",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "ApproverUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Comments",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "DecidedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "EvaluationReportId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Round",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Sequence",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Stage",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Approvals",
      "indexes": [
        {
          "name": "IX_Approvals_ApproverUserId",
          "columns": [
            "ApproverUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_Approvals_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_Approvals_EvaluationReportId",
          "columns": [
            "EvaluationReportId"
          ],
          "unique": false
        },
        {
          "name": "IX_Approvals_TenderId_Round_Sequence",
          "columns": [
            "TenderId",
            "Round",
            "Sequence"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "AspNetRoleClaims",
      "group": "identity",
      "description": "ASP.NET Role Claims records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ClaimType",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ClaimValue",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "RoleId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_AspNetRoleClaims",
      "indexes": [
        {
          "name": "IX_AspNetRoleClaims_RoleId",
          "columns": [
            "RoleId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "AspNetRoles",
      "group": "identity",
      "description": "ASP.NET Roles records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "nvarchar(450)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ConcurrencyStamp",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Name",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 256
        },
        {
          "name": "NormalizedName",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": true,
          "maxLength": 256
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_AspNetRoles",
      "indexes": [
        {
          "name": "RoleNameIndex",
          "columns": [
            "NormalizedName"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "AspNetUserClaims",
      "group": "identity",
      "description": "ASP.NET User Claims records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ClaimType",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ClaimValue",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_AspNetUserClaims",
      "indexes": [
        {
          "name": "IX_AspNetUserClaims_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "AspNetUserLogins",
      "group": "identity",
      "description": "ASP.NET User Logins records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "LoginProvider",
          "type": "nvarchar(128)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 128
        },
        {
          "name": "ProviderKey",
          "type": "nvarchar(128)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 128
        },
        {
          "name": "ProviderDisplayName",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "LoginProvider",
        "ProviderKey"
      ],
      "primaryKeyName": "PK_AspNetUserLogins",
      "indexes": [
        {
          "name": "IX_AspNetUserLogins_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "AspNetUserRoles",
      "group": "identity",
      "description": "ASP.NET User Roles records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": true,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "RoleId",
          "type": "nvarchar(450)",
          "pk": true,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "UserId",
        "RoleId"
      ],
      "primaryKeyName": "PK_AspNetUserRoles",
      "indexes": [
        {
          "name": "IX_AspNetUserRoles_RoleId",
          "columns": [
            "RoleId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "AspNetUsers",
      "group": "identity",
      "description": "ASP.NET Users records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "nvarchar(450)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AccessFailedCount",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ConcurrencyStamp",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DepartmentId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Email",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 256
        },
        {
          "name": "EmailConfirmed",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "FullName",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 150
        },
        {
          "name": "IsActive",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "LastLoginAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "LockoutEnabled",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "LockoutEnd",
          "type": "datetimeoffset",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "NormalizedEmail",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 256
        },
        {
          "name": "NormalizedUserName",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": true,
          "maxLength": 256
        },
        {
          "name": "PasswordHash",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "PhoneNumber",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "PhoneNumberConfirmed",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SecurityStamp",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "TwoFactorEnabled",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "UserName",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 256
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_AspNetUsers",
      "indexes": [
        {
          "name": "IX_AspNetUsers_DepartmentId",
          "columns": [
            "DepartmentId"
          ],
          "unique": false
        },
        {
          "name": "EmailIndex",
          "columns": [
            "NormalizedEmail"
          ],
          "unique": false
        },
        {
          "name": "UserNameIndex",
          "columns": [
            "NormalizedUserName"
          ],
          "unique": true
        },
        {
          "name": "IX_AspNetUsers_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "AspNetUserTokens",
      "group": "identity",
      "description": "ASP.NET User Tokens records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": true,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "LoginProvider",
          "type": "nvarchar(128)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 128
        },
        {
          "name": "Name",
          "type": "nvarchar(128)",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 128
        },
        {
          "name": "Value",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "UserId",
        "LoginProvider",
        "Name"
      ],
      "primaryKeyName": "PK_AspNetUserTokens",
      "indexes": []
    },
    {
      "name": "AuditLogs",
      "group": "governance",
      "description": "Security and business audit trail for important system actions.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Action",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EntityId",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "EntityName",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 150
        },
        {
          "name": "IpAddress",
          "type": "nvarchar(50)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 50
        },
        {
          "name": "NewValues",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "OldValues",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_AuditLogs",
      "indexes": [
        {
          "name": "IX_AuditLogs_CreatedAt",
          "columns": [
            "CreatedAt"
          ],
          "unique": false
        },
        {
          "name": "IX_AuditLogs_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        },
        {
          "name": "IX_AuditLogs_EntityName_EntityId",
          "columns": [
            "EntityName",
            "EntityId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "BidAnalyses",
      "group": "bids",
      "description": "Saved AI-assisted comparison and risk analysis for tender bids.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AnalyzedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "ExtractedDataJson",
          "type": "nvarchar(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ModelName",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "PreliminaryRecommendation",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "RequestedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "Strengths",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "Weaknesses",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidAnalyses",
      "indexes": [
        {
          "name": "IX_BidAnalyses_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidAnalyses_RequestedByUserId",
          "columns": [
            "RequestedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "BidComplianceReviews",
      "group": "bids",
      "description": "Bid Compliance Reviews records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Decision",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "ReviewedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ReviewedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "Round",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidComplianceReviews",
      "indexes": [
        {
          "name": "IX_BidComplianceReviews_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidComplianceReviews_ReviewedByUserId",
          "columns": [
            "ReviewedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "BidDocuments",
      "group": "bids",
      "description": "Bid Documents records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "ContentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "DocumentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "FileName",
          "type": "nvarchar(255)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 255
        },
        {
          "name": "FilePath",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 1000
        },
        {
          "name": "FileSize",
          "type": "bigint",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "IsVerified",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderDocumentRequirementId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "UploadedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidDocuments",
      "indexes": [
        {
          "name": "IX_BidDocuments_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidDocuments_TenderDocumentRequirementId",
          "columns": [
            "TenderDocumentRequirementId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "BidEvaluations",
      "group": "bids",
      "description": "Bid Evaluations records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Comments",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "EvaluatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EvaluatorUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "IsRecommended",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TotalScore",
          "type": "decimal(7,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidEvaluations",
      "indexes": [
        {
          "name": "IX_BidEvaluations_EvaluatorUserId",
          "columns": [
            "EvaluatorUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidEvaluations_BidId_EvaluatorUserId",
          "columns": [
            "BidId",
            "EvaluatorUserId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "BidEvaluationScores",
      "group": "bids",
      "description": "Bid Evaluation Scores records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidEvaluationId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Comment",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "EvaluationCriterionId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Score",
          "type": "decimal(7,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidEvaluationScores",
      "indexes": [
        {
          "name": "IX_BidEvaluationScores_EvaluationCriterionId",
          "columns": [
            "EvaluationCriterionId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidEvaluationScores_BidEvaluationId_EvaluationCriterionId",
          "columns": [
            "BidEvaluationId",
            "EvaluationCriterionId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "BidItems",
      "group": "bids",
      "description": "Bid Items records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "BrandOrModel",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "TechnicalResponse",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "TenderItemId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "TotalPrice",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "UnitPrice",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_BidItems",
      "indexes": [
        {
          "name": "IX_BidItems_TenderItemId",
          "columns": [
            "TenderItemId"
          ],
          "unique": false
        },
        {
          "name": "IX_BidItems_BidId_TenderItemId",
          "columns": [
            "BidId",
            "TenderItemId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "Bids",
      "group": "bids",
      "description": "Supplier offers submitted for a tender, including price and evaluation state.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidNumber",
          "type": "nvarchar(50)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 50
        },
        {
          "name": "ComplianceStatus",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Currency",
          "type": "nvarchar(3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 3
        },
        {
          "name": "DeliveryDays",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DisqualificationReason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "FeesAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "InstallationAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "MaintenanceAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PaymentTerms",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "ShippingAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SubmittedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "TaxAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TechnicalSummary",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "TotalAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ValidUntil",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Bids",
      "indexes": [
        {
          "name": "IX_Bids_BidNumber",
          "columns": [
            "BidNumber"
          ],
          "unique": true
        },
        {
          "name": "IX_Bids_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        },
        {
          "name": "IX_Bids_TenderId_SupplierId",
          "columns": [
            "TenderId",
            "SupplierId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "Categories",
      "group": "requests",
      "description": "Categories records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "Name",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 150
        },
        {
          "name": "ParentCategoryId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Categories",
      "indexes": [
        {
          "name": "IX_Categories_ParentCategoryId_Name",
          "columns": [
            "ParentCategoryId",
            "Name"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "ClarificationResponses",
      "group": "bids",
      "description": "Clarification Responses records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ClarificationId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "RespondedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "RespondedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        },
        {
          "name": "ResponseText",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 3000
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ClarificationResponses",
      "indexes": [
        {
          "name": "IX_ClarificationResponses_ClarificationId",
          "columns": [
            "ClarificationId"
          ],
          "unique": false
        },
        {
          "name": "IX_ClarificationResponses_RespondedByUserId",
          "columns": [
            "RespondedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "Clarifications",
      "group": "bids",
      "description": "Clarifications records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AskedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AskedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "DueAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Question",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "RecipientUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        },
        {
          "name": "ResolvedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Subject",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Clarifications",
      "indexes": [
        {
          "name": "IX_Clarifications_AskedByUserId",
          "columns": [
            "AskedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_Clarifications_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_Clarifications_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        },
        {
          "name": "IX_Clarifications_RecipientUserId",
          "columns": [
            "RecipientUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_Clarifications_TenderId",
          "columns": [
            "TenderId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "CommitteePositions",
      "group": "bids",
      "description": "Committee Positions records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CommitteeType",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EffectiveFrom",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EffectiveTo",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "IsActive",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PositionName",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_CommitteePositions",
      "indexes": [
        {
          "name": "IX_CommitteePositions_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        },
        {
          "name": "IX_CommitteePositions_CommitteeType_PositionName_IsActive",
          "columns": [
            "CommitteeType",
            "PositionName",
            "IsActive"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "CommitteeVotes",
      "group": "bids",
      "description": "Top committee decisions and voting rationale for a tender.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CommitteeType",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DecidedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Decision",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EvaluationReportId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "MemberUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_CommitteeVotes",
      "indexes": [
        {
          "name": "IX_CommitteeVotes_MemberUserId",
          "columns": [
            "MemberUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_CommitteeVotes_EvaluationReportId_CommitteeType_MemberUserId",
          "columns": [
            "EvaluationReportId",
            "CommitteeType",
            "MemberUserId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "ContactMessages",
      "group": "governance",
      "description": "Contact Messages records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Email",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 256
        },
        {
          "name": "IsRead",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Message",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "Name",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 150
        },
        {
          "name": "ReadAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ContactMessages",
      "indexes": []
    },
    {
      "name": "ContractMilestones",
      "group": "fulfilment",
      "description": "Contract Milestones records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CompletedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ContractId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "DueDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PaymentAmount",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Title",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ContractMilestones",
      "indexes": [
        {
          "name": "IX_ContractMilestones_ContractId",
          "columns": [
            "ContractId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "Contracts",
      "group": "fulfilment",
      "description": "Awarded supplier contract and commercial execution details.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": true,
          "null": false
        },
        {
          "name": "ContractNumber",
          "type": "nvarchar(50)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 50
        },
        {
          "name": "Currency",
          "type": "nvarchar(3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 3
        },
        {
          "name": "EndDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "StartDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "TotalValue",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Contracts",
      "indexes": [
        {
          "name": "IX_Contracts_BidId",
          "columns": [
            "BidId"
          ],
          "unique": true
        },
        {
          "name": "IX_Contracts_ContractNumber",
          "columns": [
            "ContractNumber"
          ],
          "unique": true
        },
        {
          "name": "IX_Contracts_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "Deliveries",
      "group": "tenders",
      "description": "Delivery records and acceptance status against purchase orders.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DeliveredAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DeliveryNumber",
          "type": "nvarchar(50)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 50
        },
        {
          "name": "InspectedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Notes",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "PurchaseOrderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "ReceivedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Deliveries",
      "indexes": [
        {
          "name": "IX_Deliveries_DeliveryNumber",
          "columns": [
            "DeliveryNumber"
          ],
          "unique": true
        },
        {
          "name": "IX_Deliveries_PurchaseOrderId",
          "columns": [
            "PurchaseOrderId"
          ],
          "unique": false
        },
        {
          "name": "IX_Deliveries_ReceivedByUserId",
          "columns": [
            "ReceivedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "DeliveryItems",
      "group": "fulfilment",
      "description": "Delivery Items records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AcceptedQuantity",
          "type": "decimal(18,3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DeliveryId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "InspectionNotes",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "PurchaseOrderItemId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "ReceivedQuantity",
          "type": "decimal(18,3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_DeliveryItems",
      "indexes": [
        {
          "name": "IX_DeliveryItems_PurchaseOrderItemId",
          "columns": [
            "PurchaseOrderItemId"
          ],
          "unique": false
        },
        {
          "name": "IX_DeliveryItems_DeliveryId_PurchaseOrderItemId",
          "columns": [
            "DeliveryId",
            "PurchaseOrderItemId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "Departments",
      "group": "requests",
      "description": "Departments records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Code",
          "type": "nvarchar(30)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": true,
          "maxLength": 30
        },
        {
          "name": "IsActive",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Name",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 150
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Departments",
      "indexes": [
        {
          "name": "IX_Departments_Code",
          "columns": [
            "Code"
          ],
          "unique": true
        },
        {
          "name": "IX_Departments_Name",
          "columns": [
            "Name"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "EvaluationCriteria",
      "group": "tenders",
      "description": "Evaluation Criteria records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "MaximumScore",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Name",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Type",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Weight",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_EvaluationCriteria",
      "indexes": [
        {
          "name": "IX_EvaluationCriteria_TenderId_Name",
          "columns": [
            "TenderId",
            "Name"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "EvaluationReportBids",
      "group": "bids",
      "description": "Evaluation Report Bids records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "EvaluationReportId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "FinancialScore",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "IsCompliant",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Notes",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "TechnicalScore",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "WeightedTotal",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_EvaluationReportBids",
      "indexes": [
        {
          "name": "IX_EvaluationReportBids_BidId",
          "columns": [
            "BidId"
          ],
          "unique": false
        },
        {
          "name": "IX_EvaluationReportBids_EvaluationReportId_BidId",
          "columns": [
            "EvaluationReportId",
            "BidId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "EvaluationReports",
      "group": "bids",
      "description": "Committee recommendation submitted for management and final review.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PreparedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "RecommendationReason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "RecommendedBidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Round",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SubmittedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Summary",
          "type": "nvarchar(4000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 4000
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_EvaluationReports",
      "indexes": [
        {
          "name": "IX_EvaluationReports_PreparedByUserId",
          "columns": [
            "PreparedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_EvaluationReports_RecommendedBidId",
          "columns": [
            "RecommendedBidId"
          ],
          "unique": false
        },
        {
          "name": "IX_EvaluationReports_TenderId_Round",
          "columns": [
            "TenderId",
            "Round"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "Notifications",
      "group": "identity",
      "description": "Notifications records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "IsRead",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Link",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "Message",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "ReadAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Title",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        },
        {
          "name": "Type",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Notifications",
      "indexes": [
        {
          "name": "IX_Notifications_UserId_IsRead",
          "columns": [
            "UserId",
            "IsRead"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "ProcessMessageAttachments",
      "group": "tenders",
      "description": "Process Message Attachments records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ContentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "FileName",
          "type": "nvarchar(255)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 255
        },
        {
          "name": "FilePath",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 1000
        },
        {
          "name": "FileSize",
          "type": "bigint",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ProcessMessageId",
          "type": "bigint",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ProcessMessageAttachments",
      "indexes": [
        {
          "name": "IX_ProcessMessageAttachments_ProcessMessageId",
          "columns": [
            "ProcessMessageId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "ProcessMessages",
      "group": "tenders",
      "description": "Process Messages records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Body",
          "type": "nvarchar(4000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 4000
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "ReadAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "RecipientUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "SenderUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "SentAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ProcessMessages",
      "indexes": [
        {
          "name": "IX_ProcessMessages_RecipientUserId",
          "columns": [
            "RecipientUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcessMessages_SenderUserId",
          "columns": [
            "SenderUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcessMessages_BidId_SentAt",
          "columns": [
            "BidId",
            "SentAt"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcessMessages_ProcurementRequestId_SentAt",
          "columns": [
            "ProcurementRequestId",
            "SentAt"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcessMessages_TenderId_SentAt",
          "columns": [
            "TenderId",
            "SentAt"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "ProcurementRequestItems",
      "group": "requests",
      "description": "Procurement Request Items records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CategoryId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "EstimatedUnitPrice",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ItemName",
          "type": "nvarchar(250)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 250
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Quantity",
          "type": "decimal(18,3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Specifications",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "Unit",
          "type": "nvarchar(30)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 30
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ProcurementRequestItems",
      "indexes": [
        {
          "name": "IX_ProcurementRequestItems_CategoryId",
          "columns": [
            "CategoryId"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcurementRequestItems_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "ProcurementRequestReviews",
      "group": "requests",
      "description": "Procurement Request Reviews records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Decision",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "ReviewedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ReviewerUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ProcurementRequestReviews",
      "indexes": [
        {
          "name": "IX_ProcurementRequestReviews_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcurementRequestReviews_ReviewerUserId",
          "columns": [
            "ReviewerUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "ProcurementRequests",
      "group": "requests",
      "description": "Department purchasing requests that begin the sourcing workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "DepartmentId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "EstimatedBudget",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Priority",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "RequestNumber",
          "type": "nvarchar(40)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 40
        },
        {
          "name": "RequestedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "RequestedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "RequiredByDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Title",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_ProcurementRequests",
      "indexes": [
        {
          "name": "IX_ProcurementRequests_DepartmentId",
          "columns": [
            "DepartmentId"
          ],
          "unique": false
        },
        {
          "name": "IX_ProcurementRequests_RequestNumber",
          "columns": [
            "RequestNumber"
          ],
          "unique": true
        },
        {
          "name": "IX_ProcurementRequests_RequestedByUserId",
          "columns": [
            "RequestedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "PurchaseOrderItems",
      "group": "fulfilment",
      "description": "Purchase Order Items records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "BidItemId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "PurchaseOrderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Quantity",
          "type": "decimal(18,3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "UnitPrice",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_PurchaseOrderItems",
      "indexes": [
        {
          "name": "IX_PurchaseOrderItems_BidItemId",
          "columns": [
            "BidItemId"
          ],
          "unique": false
        },
        {
          "name": "IX_PurchaseOrderItems_PurchaseOrderId",
          "columns": [
            "PurchaseOrderId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "PurchaseOrders",
      "group": "fulfilment",
      "description": "Orders raised from approved contracts.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ContractId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "ExpectedDeliveryDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "IssuedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PurchaseOrderNumber",
          "type": "nvarchar(50)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 50
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_PurchaseOrders",
      "indexes": [
        {
          "name": "IX_PurchaseOrders_ContractId",
          "columns": [
            "ContractId"
          ],
          "unique": false
        },
        {
          "name": "IX_PurchaseOrders_PurchaseOrderNumber",
          "columns": [
            "PurchaseOrderNumber"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "SupplierCategories",
      "group": "suppliers",
      "description": "Supplier Categories records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "SupplierId",
          "type": "int",
          "pk": true,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "CategoryId",
          "type": "int",
          "pk": true,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "SupplierId",
        "CategoryId"
      ],
      "primaryKeyName": "PK_SupplierCategories",
      "indexes": [
        {
          "name": "IX_SupplierCategories_CategoryId",
          "columns": [
            "CategoryId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "SupplierDocuments",
      "group": "suppliers",
      "description": "Supplier Documents records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ContentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "DocumentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "ExpiresAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "FileName",
          "type": "nvarchar(255)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 255
        },
        {
          "name": "FilePath",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 1000
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "UploadedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "VerificationNotes",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "VerificationStatus",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "VerifiedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "VerifiedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_SupplierDocuments",
      "indexes": [
        {
          "name": "IX_SupplierDocuments_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        },
        {
          "name": "IX_SupplierDocuments_VerifiedByUserId",
          "columns": [
            "VerifiedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "SupplierReviews",
      "group": "suppliers",
      "description": "Supplier Reviews records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Decision",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 2000
        },
        {
          "name": "ReviewedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ReviewedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_SupplierReviews",
      "indexes": [
        {
          "name": "IX_SupplierReviews_ReviewedByUserId",
          "columns": [
            "ReviewedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_SupplierReviews_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "Suppliers",
      "group": "suppliers",
      "description": "Registered supplier profiles, compliance details and account ownership.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Address",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "CompanyName",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        },
        {
          "name": "Email",
          "type": "nvarchar(256)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 256
        },
        {
          "name": "IsActive",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "LegalVerificationStatus",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "LicenseExpiresAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "LicenseNumber",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "PhoneNumber",
          "type": "nvarchar(30)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 30
        },
        {
          "name": "RegisteredAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "RegistrationNumber",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "RegistrationStatus",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ReviewReason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "ReviewedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ReviewedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        },
        {
          "name": "TaxNumber",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Suppliers",
      "indexes": [
        {
          "name": "IX_Suppliers_Email",
          "columns": [
            "Email"
          ],
          "unique": true
        },
        {
          "name": "IX_Suppliers_RegistrationNumber",
          "columns": [
            "RegistrationNumber"
          ],
          "unique": true
        },
        {
          "name": "IX_Suppliers_ReviewedByUserId",
          "columns": [
            "ReviewedByUserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "TenderCommitteeMembers",
      "group": "tenders",
      "description": "Tender Committee Members records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CommitteeType",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "IsChairperson",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Role",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "RoleType",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_TenderCommitteeMembers",
      "indexes": [
        {
          "name": "IX_TenderCommitteeMembers_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        },
        {
          "name": "IX_TenderCommitteeMembers_TenderId_CommitteeType_Role",
          "columns": [
            "TenderId",
            "CommitteeType",
            "Role"
          ],
          "unique": true
        },
        {
          "name": "IX_TenderCommitteeMembers_TenderId_CommitteeType_UserId",
          "columns": [
            "TenderId",
            "CommitteeType",
            "UserId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "TenderDocumentRequirements",
      "group": "tenders",
      "description": "Tender Document Requirements records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "DocumentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 100
        },
        {
          "name": "IsMandatory",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_TenderDocumentRequirements",
      "indexes": [
        {
          "name": "IX_TenderDocumentRequirements_TenderId_DocumentType",
          "columns": [
            "TenderId",
            "DocumentType"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "TenderDocuments",
      "group": "tenders",
      "description": "Tender Documents records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ContentType",
          "type": "nvarchar(100)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 100
        },
        {
          "name": "FileName",
          "type": "nvarchar(255)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 255
        },
        {
          "name": "FilePath",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 1000
        },
        {
          "name": "FileSize",
          "type": "bigint",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "UploadedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_TenderDocuments",
      "indexes": [
        {
          "name": "IX_TenderDocuments_TenderId",
          "columns": [
            "TenderId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "TenderInvitations",
      "group": "tenders",
      "description": "Tender Invitations records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "InvitedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "InvitedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "RespondedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ResponseReason",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SupplierId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_TenderInvitations",
      "indexes": [
        {
          "name": "IX_TenderInvitations_InvitedByUserId",
          "columns": [
            "InvitedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_TenderInvitations_SupplierId",
          "columns": [
            "SupplierId"
          ],
          "unique": false
        },
        {
          "name": "IX_TenderInvitations_TenderId_SupplierId",
          "columns": [
            "TenderId",
            "SupplierId"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "TenderItems",
      "group": "tenders",
      "description": "Tender Items records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "ItemName",
          "type": "nvarchar(250)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 250
        },
        {
          "name": "Quantity",
          "type": "decimal(18,3)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Specifications",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "Unit",
          "type": "nvarchar(30)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 30
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_TenderItems",
      "indexes": [
        {
          "name": "IX_TenderItems_TenderId",
          "columns": [
            "TenderId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "Tenders",
      "group": "tenders",
      "description": "Core sourcing event with its schedule, value, workflow status and selected award.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AwardedBidId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "CategoryId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CreatedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "Description",
          "type": "nvarchar(3000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 3000
        },
        {
          "name": "EstimatedValue",
          "type": "decimal(18,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "EvaluationCriteriaLockedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "FinancialWeight",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "OpeningDate",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "PublishedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "RequiresTopCommittee",
          "type": "bit",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SubmissionDeadline",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TechnicalWeight",
          "type": "decimal(5,2)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderNumber",
          "type": "nvarchar(40)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 40
        },
        {
          "name": "TermsAndConditions",
          "type": "nvarchar(4000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 4000
        },
        {
          "name": "Title",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_Tenders",
      "indexes": [
        {
          "name": "IX_Tenders_AwardedBidId",
          "columns": [
            "AwardedBidId"
          ],
          "unique": false
        },
        {
          "name": "IX_Tenders_CategoryId",
          "columns": [
            "CategoryId"
          ],
          "unique": false
        },
        {
          "name": "IX_Tenders_CreatedByUserId",
          "columns": [
            "CreatedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_Tenders_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        },
        {
          "name": "IX_Tenders_TenderNumber",
          "columns": [
            "TenderNumber"
          ],
          "unique": true
        }
      ]
    },
    {
      "name": "UserPasskeyCredentials",
      "group": "identity",
      "description": "User Passkey Credentials records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AddedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CredentialId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": false,
          "uq": true,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "DeviceLabel",
          "type": "nvarchar(150)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 150
        },
        {
          "name": "LastUsedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "PublicKey",
          "type": "varbinary(max)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "SignCount",
          "type": "bigint",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_UserPasskeyCredentials",
      "indexes": [
        {
          "name": "IX_UserPasskeyCredentials_CredentialId",
          "columns": [
            "CredentialId"
          ],
          "unique": true
        },
        {
          "name": "IX_UserPasskeyCredentials_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "UserTasks",
      "group": "identity",
      "description": "User Tasks records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "CompletedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Description",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "DueAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Link",
          "type": "nvarchar(500)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 500
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Status",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Title",
          "type": "nvarchar(200)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false,
          "maxLength": 200
        },
        {
          "name": "UserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_UserTasks",
      "indexes": [
        {
          "name": "IX_UserTasks_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        },
        {
          "name": "IX_UserTasks_TenderId",
          "columns": [
            "TenderId"
          ],
          "unique": false
        },
        {
          "name": "IX_UserTasks_UserId",
          "columns": [
            "UserId"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "WorkAssignments",
      "group": "requests",
      "description": "Work Assignments records used by the sourcing and tender management workflow.",
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AssignedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AssignedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "AssignedToUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "CompletedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true
        },
        {
          "name": "Kind",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Notes",
          "type": "nvarchar(1000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 1000
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_WorkAssignments",
      "indexes": [
        {
          "name": "IX_WorkAssignments_AssignedByUserId",
          "columns": [
            "AssignedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkAssignments_ProcurementRequestId",
          "columns": [
            "ProcurementRequestId"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkAssignments_TenderId",
          "columns": [
            "TenderId"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkAssignments_AssignedToUserId_CompletedAt",
          "columns": [
            "AssignedToUserId",
            "CompletedAt"
          ],
          "unique": false
        }
      ]
    },
    {
      "name": "WorkflowTransitions",
      "group": "tenders",
      "description": "Immutable history of tender status changes and responsible actors.",
      "columns": [
        {
          "name": "Id",
          "type": "bigint",
          "pk": true,
          "fk": false,
          "uq": false,
          "null": false,
          "identity": true
        },
        {
          "name": "Id",
          "type": "long",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "Action",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "AssignedToUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true,
          "maxLength": 450
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "FromStage",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "PerformedByUserId",
          "type": "nvarchar(450)",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": false,
          "maxLength": 450
        },
        {
          "name": "ProcurementRequestId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "Reason",
          "type": "nvarchar(2000)",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": true,
          "maxLength": 2000
        },
        {
          "name": "Round",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        },
        {
          "name": "TenderId",
          "type": "int",
          "pk": false,
          "fk": true,
          "uq": false,
          "null": true
        },
        {
          "name": "ToStage",
          "type": "int",
          "pk": false,
          "fk": false,
          "uq": false,
          "null": false
        }
      ],
      "primaryKey": [
        "Id"
      ],
      "primaryKeyName": "PK_WorkflowTransitions",
      "indexes": [
        {
          "name": "IX_WorkflowTransitions_AssignedToUserId",
          "columns": [
            "AssignedToUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkflowTransitions_PerformedByUserId",
          "columns": [
            "PerformedByUserId"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkflowTransitions_ProcurementRequestId_CreatedAt",
          "columns": [
            "ProcurementRequestId",
            "CreatedAt"
          ],
          "unique": false
        },
        {
          "name": "IX_WorkflowTransitions_TenderId_CreatedAt",
          "columns": [
            "TenderId",
            "CreatedAt"
          ],
          "unique": false
        }
      ]
    }
  ],
  "relationships": [
    {
      "name": "FK_Approvals_AspNetUsers_ApproverUserId",
      "from": "Approvals",
      "fromColumns": [
        "ApproverUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Approvals_Bids_BidId",
      "from": "Approvals",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Approvals_EvaluationReports_EvaluationReportId",
      "from": "Approvals",
      "fromColumns": [
        "EvaluationReportId"
      ],
      "to": "EvaluationReports",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Approvals_Tenders_TenderId",
      "from": "Approvals",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetRoleClaims_AspNetRoles_RoleId",
      "from": "AspNetRoleClaims",
      "fromColumns": [
        "RoleId"
      ],
      "to": "AspNetRoles",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetUserClaims_AspNetUsers_UserId",
      "from": "AspNetUserClaims",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetUserLogins_AspNetUsers_UserId",
      "from": "AspNetUserLogins",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetUserRoles_AspNetRoles_RoleId",
      "from": "AspNetUserRoles",
      "fromColumns": [
        "RoleId"
      ],
      "to": "AspNetRoles",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetUserRoles_AspNetUsers_UserId",
      "from": "AspNetUserRoles",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AspNetUsers_Departments_DepartmentId",
      "from": "AspNetUsers",
      "fromColumns": [
        "DepartmentId"
      ],
      "to": "Departments",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_AspNetUsers_Suppliers_SupplierId",
      "from": "AspNetUsers",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_AspNetUserTokens_AspNetUsers_UserId",
      "from": "AspNetUserTokens",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_AuditLogs_AspNetUsers_UserId",
      "from": "AuditLogs",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "SetNull",
      "required": false
    },
    {
      "name": "FK_BidAnalyses_AspNetUsers_RequestedByUserId",
      "from": "BidAnalyses",
      "fromColumns": [
        "RequestedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidAnalyses_Bids_BidId",
      "from": "BidAnalyses",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidComplianceReviews_AspNetUsers_ReviewedByUserId",
      "from": "BidComplianceReviews",
      "fromColumns": [
        "ReviewedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidComplianceReviews_Bids_BidId",
      "from": "BidComplianceReviews",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidDocuments_Bids_BidId",
      "from": "BidDocuments",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_BidDocuments_TenderDocumentRequirements_TenderDocumentRequirementId",
      "from": "BidDocuments",
      "fromColumns": [
        "TenderDocumentRequirementId"
      ],
      "to": "TenderDocumentRequirements",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": false
    },
    {
      "name": "FK_BidEvaluations_AspNetUsers_EvaluatorUserId",
      "from": "BidEvaluations",
      "fromColumns": [
        "EvaluatorUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidEvaluations_Bids_BidId",
      "from": "BidEvaluations",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_BidEvaluationScores_BidEvaluations_BidEvaluationId",
      "from": "BidEvaluationScores",
      "fromColumns": [
        "BidEvaluationId"
      ],
      "to": "BidEvaluations",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_BidEvaluationScores_EvaluationCriteria_EvaluationCriterionId",
      "from": "BidEvaluationScores",
      "fromColumns": [
        "EvaluationCriterionId"
      ],
      "to": "EvaluationCriteria",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_BidItems_Bids_BidId",
      "from": "BidItems",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_BidItems_TenderItems_TenderItemId",
      "from": "BidItems",
      "fromColumns": [
        "TenderItemId"
      ],
      "to": "TenderItems",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Bids_Suppliers_SupplierId",
      "from": "Bids",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Bids_Tenders_TenderId",
      "from": "Bids",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Categories_Categories_ParentCategoryId",
      "from": "Categories",
      "fromColumns": [
        "ParentCategoryId"
      ],
      "to": "Categories",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_ClarificationResponses_AspNetUsers_RespondedByUserId",
      "from": "ClarificationResponses",
      "fromColumns": [
        "RespondedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_ClarificationResponses_Clarifications_ClarificationId",
      "from": "ClarificationResponses",
      "fromColumns": [
        "ClarificationId"
      ],
      "to": "Clarifications",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_Clarifications_AspNetUsers_AskedByUserId",
      "from": "Clarifications",
      "fromColumns": [
        "AskedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Clarifications_AspNetUsers_RecipientUserId",
      "from": "Clarifications",
      "fromColumns": [
        "RecipientUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Clarifications_Bids_BidId",
      "from": "Clarifications",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Clarifications_ProcurementRequests_ProcurementRequestId",
      "from": "Clarifications",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Clarifications_Tenders_TenderId",
      "from": "Clarifications",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_CommitteePositions_AspNetUsers_UserId",
      "from": "CommitteePositions",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_CommitteeVotes_AspNetUsers_MemberUserId",
      "from": "CommitteeVotes",
      "fromColumns": [
        "MemberUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_CommitteeVotes_EvaluationReports_EvaluationReportId",
      "from": "CommitteeVotes",
      "fromColumns": [
        "EvaluationReportId"
      ],
      "to": "EvaluationReports",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_ContractMilestones_Contracts_ContractId",
      "from": "ContractMilestones",
      "fromColumns": [
        "ContractId"
      ],
      "to": "Contracts",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_Contracts_Bids_BidId",
      "from": "Contracts",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Contracts_Suppliers_SupplierId",
      "from": "Contracts",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Deliveries_AspNetUsers_ReceivedByUserId",
      "from": "Deliveries",
      "fromColumns": [
        "ReceivedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Deliveries_PurchaseOrders_PurchaseOrderId",
      "from": "Deliveries",
      "fromColumns": [
        "PurchaseOrderId"
      ],
      "to": "PurchaseOrders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_DeliveryItems_Deliveries_DeliveryId",
      "from": "DeliveryItems",
      "fromColumns": [
        "DeliveryId"
      ],
      "to": "Deliveries",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_DeliveryItems_PurchaseOrderItems_PurchaseOrderItemId",
      "from": "DeliveryItems",
      "fromColumns": [
        "PurchaseOrderItemId"
      ],
      "to": "PurchaseOrderItems",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_EvaluationCriteria_Tenders_TenderId",
      "from": "EvaluationCriteria",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_EvaluationReportBids_Bids_BidId",
      "from": "EvaluationReportBids",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_EvaluationReportBids_EvaluationReports_EvaluationReportId",
      "from": "EvaluationReportBids",
      "fromColumns": [
        "EvaluationReportId"
      ],
      "to": "EvaluationReports",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_EvaluationReports_AspNetUsers_PreparedByUserId",
      "from": "EvaluationReports",
      "fromColumns": [
        "PreparedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_EvaluationReports_Bids_RecommendedBidId",
      "from": "EvaluationReports",
      "fromColumns": [
        "RecommendedBidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_EvaluationReports_Tenders_TenderId",
      "from": "EvaluationReports",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Notifications_AspNetUsers_UserId",
      "from": "Notifications",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_ProcessMessageAttachments_ProcessMessages_ProcessMessageId",
      "from": "ProcessMessageAttachments",
      "fromColumns": [
        "ProcessMessageId"
      ],
      "to": "ProcessMessages",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_ProcessMessages_AspNetUsers_RecipientUserId",
      "from": "ProcessMessages",
      "fromColumns": [
        "RecipientUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_ProcessMessages_AspNetUsers_SenderUserId",
      "from": "ProcessMessages",
      "fromColumns": [
        "SenderUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_ProcessMessages_Bids_BidId",
      "from": "ProcessMessages",
      "fromColumns": [
        "BidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_ProcessMessages_ProcurementRequests_ProcurementRequestId",
      "from": "ProcessMessages",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_ProcessMessages_Tenders_TenderId",
      "from": "ProcessMessages",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_ProcurementRequestItems_Categories_CategoryId",
      "from": "ProcurementRequestItems",
      "fromColumns": [
        "CategoryId"
      ],
      "to": "Categories",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": false
    },
    {
      "name": "FK_ProcurementRequestItems_ProcurementRequests_ProcurementRequestId",
      "from": "ProcurementRequestItems",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_ProcurementRequestReviews_AspNetUsers_ReviewerUserId",
      "from": "ProcurementRequestReviews",
      "fromColumns": [
        "ReviewerUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_ProcurementRequestReviews_ProcurementRequests_ProcurementRequestId",
      "from": "ProcurementRequestReviews",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_ProcurementRequests_AspNetUsers_RequestedByUserId",
      "from": "ProcurementRequests",
      "fromColumns": [
        "RequestedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_ProcurementRequests_Departments_DepartmentId",
      "from": "ProcurementRequests",
      "fromColumns": [
        "DepartmentId"
      ],
      "to": "Departments",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_PurchaseOrderItems_BidItems_BidItemId",
      "from": "PurchaseOrderItems",
      "fromColumns": [
        "BidItemId"
      ],
      "to": "BidItems",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
      "from": "PurchaseOrderItems",
      "fromColumns": [
        "PurchaseOrderId"
      ],
      "to": "PurchaseOrders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_PurchaseOrders_Contracts_ContractId",
      "from": "PurchaseOrders",
      "fromColumns": [
        "ContractId"
      ],
      "to": "Contracts",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_SupplierCategories_Categories_CategoryId",
      "from": "SupplierCategories",
      "fromColumns": [
        "CategoryId"
      ],
      "to": "Categories",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_SupplierCategories_Suppliers_SupplierId",
      "from": "SupplierCategories",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_SupplierDocuments_AspNetUsers_VerifiedByUserId",
      "from": "SupplierDocuments",
      "fromColumns": [
        "VerifiedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_SupplierDocuments_Suppliers_SupplierId",
      "from": "SupplierDocuments",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_SupplierReviews_AspNetUsers_ReviewedByUserId",
      "from": "SupplierReviews",
      "fromColumns": [
        "ReviewedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_SupplierReviews_Suppliers_SupplierId",
      "from": "SupplierReviews",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Suppliers_AspNetUsers_ReviewedByUserId",
      "from": "Suppliers",
      "fromColumns": [
        "ReviewedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_TenderCommitteeMembers_AspNetUsers_UserId",
      "from": "TenderCommitteeMembers",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_TenderCommitteeMembers_Tenders_TenderId",
      "from": "TenderCommitteeMembers",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_TenderDocumentRequirements_Tenders_TenderId",
      "from": "TenderDocumentRequirements",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_TenderDocuments_Tenders_TenderId",
      "from": "TenderDocuments",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_TenderInvitations_AspNetUsers_InvitedByUserId",
      "from": "TenderInvitations",
      "fromColumns": [
        "InvitedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_TenderInvitations_Suppliers_SupplierId",
      "from": "TenderInvitations",
      "fromColumns": [
        "SupplierId"
      ],
      "to": "Suppliers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_TenderInvitations_Tenders_TenderId",
      "from": "TenderInvitations",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_TenderItems_Tenders_TenderId",
      "from": "TenderItems",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_Tenders_AspNetUsers_CreatedByUserId",
      "from": "Tenders",
      "fromColumns": [
        "CreatedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Tenders_Bids_AwardedBidId",
      "from": "Tenders",
      "fromColumns": [
        "AwardedBidId"
      ],
      "to": "Bids",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_Tenders_Categories_CategoryId",
      "from": "Tenders",
      "fromColumns": [
        "CategoryId"
      ],
      "to": "Categories",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_Tenders_ProcurementRequests_ProcurementRequestId",
      "from": "Tenders",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_UserPasskeyCredentials_AspNetUsers_UserId",
      "from": "UserPasskeyCredentials",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Cascade",
      "required": true
    },
    {
      "name": "FK_UserTasks_AspNetUsers_UserId",
      "from": "UserTasks",
      "fromColumns": [
        "UserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_UserTasks_ProcurementRequests_ProcurementRequestId",
      "from": "UserTasks",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_UserTasks_Tenders_TenderId",
      "from": "UserTasks",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_WorkAssignments_AspNetUsers_AssignedByUserId",
      "from": "WorkAssignments",
      "fromColumns": [
        "AssignedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_WorkAssignments_AspNetUsers_AssignedToUserId",
      "from": "WorkAssignments",
      "fromColumns": [
        "AssignedToUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_WorkAssignments_ProcurementRequests_ProcurementRequestId",
      "from": "WorkAssignments",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_WorkAssignments_Tenders_TenderId",
      "from": "WorkAssignments",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_WorkflowTransitions_AspNetUsers_AssignedToUserId",
      "from": "WorkflowTransitions",
      "fromColumns": [
        "AssignedToUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_WorkflowTransitions_AspNetUsers_PerformedByUserId",
      "from": "WorkflowTransitions",
      "fromColumns": [
        "PerformedByUserId"
      ],
      "to": "AspNetUsers",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": true
    },
    {
      "name": "FK_WorkflowTransitions_ProcurementRequests_ProcurementRequestId",
      "from": "WorkflowTransitions",
      "fromColumns": [
        "ProcurementRequestId"
      ],
      "to": "ProcurementRequests",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    },
    {
      "name": "FK_WorkflowTransitions_Tenders_TenderId",
      "from": "WorkflowTransitions",
      "fromColumns": [
        "TenderId"
      ],
      "to": "Tenders",
      "toColumns": [
        "Id"
      ],
      "deleteBehavior": "Restrict",
      "required": false
    }
  ],
  "rules": [
    "A tender is the central record connecting requests, items, documents, invitations, bids, committees, reports and the final award.",
    "Identity tables protect users, roles, claims, logins, tokens and passkeys independently from the procurement domain.",
    "Foreign keys enforce ownership and lifecycle links; delete behavior is shown for every relationship.",
    "WorkflowTransitions and AuditLogs preserve who changed important business state and when.",
    "BidAnalyses stores AI-assisted results. Final evaluation and award decisions remain human-controlled.",
    "The EF Core migration history table is infrastructure and is intentionally excluded from this documentation."
  ]
});
