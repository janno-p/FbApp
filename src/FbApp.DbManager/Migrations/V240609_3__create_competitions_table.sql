CREATE TABLE "competitions"."Competitions" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Season" smallint NOT NULL,
    "ExternalId" integer NOT NULL,
    "BeginDateTime" timestamp NOT NULL,
    CONSTRAINT "PK_Competitions" PRIMARY KEY ("Id")
);
