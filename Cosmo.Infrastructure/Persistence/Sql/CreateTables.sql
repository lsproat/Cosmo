-- PostgreSQL schema matching CosmoDbContext. Run after dropping the old tables.
-- IDs, UTC timestamps, and zero-based sequence numbers are assigned by the app.
-- Role values: 0 = System, 1 = User, 2 = Assistant.

BEGIN;

CREATE TABLE "Conversations" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Title" text NOT NULL,
    CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id")
);

CREATE TABLE "ConversationMessages" (
    "Id" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "Role" integer NOT NULL,
    "Content" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ConversationId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ConversationMessages_Conversations_ConversationId"
        FOREIGN KEY ("ConversationId") REFERENCES "Conversations" ("Id")
        ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_ConversationMessages_ConversationId_SequenceNumber"
    ON "ConversationMessages" ("ConversationId", "SequenceNumber");

COMMIT;
