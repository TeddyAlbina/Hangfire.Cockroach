CREATE SCHEMA IF NOT EXISTS "hangfire";

SET search_path = 'hangfire';

--
-- Table structure for table `Schema`
--

CREATE TABLE IF NOT EXISTS "schema"
(
    "version" INT NOT NULL,
    PRIMARY KEY ("version")
);

INSERT INTO "schema"("version") VALUES ('21');


--
-- Table structure for table `Counter`
--

CREATE TABLE IF NOT EXISTS "counter"
(
    "id"       uuid    default gen_random_uuid() not null,
    "key"      TEXT NOT NULL,
    "value"    bigint     NOT NULL,
    "expireat" timestamptz    NULL,
    PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "ix_hangfire_counter_key" ON "counter" ("key");

--
-- Table structure for table `Hash`
--

CREATE TABLE IF NOT EXISTS "hash"
(
    "id"       uuid    default gen_random_uuid() not null,
    "key"      TEXT NOT NULL,
    "field"    TEXT NOT NULL,
    "value"    TEXT         NULL,
    "expireat" timestamptz    NULL,
    "acquired" timestamp without time zone,
    "updatecount" integer NOT NULL DEFAULT 0,
    PRIMARY KEY ("id"),
    UNIQUE ("key", "field")
);


--
-- Table structure for table `Job`
--

CREATE TABLE IF NOT EXISTS "job"
(
    "id"             uuid    default gen_random_uuid() not null,
    "stateid"        uuid         NULL,
    "statename"      TEXT NULL,
    "invocationdata" jsonb        NOT NULL,
    "arguments"      jsonb        NOT NULL,
    "createdat"      timestamptz   NOT NULL,
    "expireat"       timestamptz   NULL,
    "updatecount"    integer NOT NULL DEFAULT 0,
    "insertedat" timestamptz NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "ix_hangfire_job_statename" ON "job" ("statename");

--
-- Table structure for table `State`
--

CREATE TABLE IF NOT EXISTS "state"
(
    "id"        uuid    default gen_random_uuid() not null,
    "jobid"     uuid          NOT NULL,
    "name"      TEXT  NOT NULL,
    "reason"    TEXT NULL,
    "createdat" timestamptz    NOT NULL,
    "data"      jsonb         NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    "serialid" SERIAL NOT NULL,
    PRIMARY KEY ("id"),
    FOREIGN KEY ("jobid") REFERENCES "job" ("id") ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "ix_hangfire_state_jobid" ON "state" ("jobid");


--
-- Table structure for table `JobQueue`
--

CREATE TABLE IF NOT EXISTS "jobqueue"
(
    "id"        uuid    default gen_random_uuid() not null,
    "jobid"     uuid         NOT NULL,
    "queue"     TEXT NOT NULL,
    "fetchedat" timestamptz   NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    "createdat" timestamptz NOT NULL DEFAULT NOW(),
    "serialid" SERIAL NOT NULL,
    PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "ix_hangfire_jobqueue_queueandfetchedat" ON "jobqueue" ("queue", "fetchedat");
--
-- Table structure for table `List`
--

CREATE TABLE IF NOT EXISTS "list"
(
    "id"       uuid    default gen_random_uuid() not null,
    "key"      TEXT NOT NULL,
    "value"    TEXT         NULL,
    "expireat" timestamptz    NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    "createdat" timestamptz NOT NULL DEFAULT NOW(),
    "serialid" SERIAL NOT NULL,
    PRIMARY KEY ("id")
);


--
-- Table structure for table `Server`
--

CREATE TABLE IF NOT EXISTS "server"
(
    "id"            TEXT    not null,
    "data"          jsonb        NULL,
    "lastheartbeat" timestamptz   NOT NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    PRIMARY KEY ("id")
);


--
-- Table structure for table `Set`
--

CREATE TABLE IF NOT EXISTS "set"
(
    "id"       uuid    default gen_random_uuid() not null,
    "key"      TEXT NOT NULL,
    "score"    FLOAT8       NOT NULL,
    "value"    TEXT         NOT NULL,
    "expireat" timestamptz    NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    "createdat" timestamptz NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("id"),
    UNIQUE ("key", "value")
);


--
-- Table structure for table `JobParameter`
--

CREATE TABLE IF NOT EXISTS "jobparameter"
(
    "id"    uuid    default gen_random_uuid() not null,
    "jobid" uuid         NOT NULL,
    "name"  TEXT NOT NULL,
    "value" TEXT        NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    PRIMARY KEY ("id"),
    FOREIGN KEY ("jobid") REFERENCES "job" ("id") ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "ix_hangfire_jobparameter_jobidandname" ON "jobparameter" ("jobid", "name");

CREATE TABLE IF NOT EXISTS "lock"
(
    "resource" TEXT NOT NULL,
    "updatecount" integer NOT NULL DEFAULT 0,
    "acquired"  timestamptz NOT NULL,
    UNIQUE ("resource")
);


CREATE INDEX IF NOT EXISTS "ix_hangfire_counter_expireat" ON "counter" ("expireat");
CREATE INDEX IF NOT EXISTS "ix_hangfire_jobqueue_jobidandqueue" ON "jobqueue" ("jobid", "queue");
CREATE INDEX IF NOT EXISTS jobqueue_queue_fetchat_jobId ON jobqueue USING btree (queue asc, fetchedat asc, jobid asc);
CREATE INDEX IF NOT EXISTS ix_hangfire_job_expireat ON "job" (expireat);
CREATE INDEX IF NOT EXISTS ix_hangfire_list_expireat ON "list" (expireat);
CREATE INDEX IF NOT EXISTS ix_hangfire_set_expireat ON "set" (expireat);
CREATE INDEX IF NOT EXISTS ix_hangfire_hash_expireat ON "hash" (expireat);
CREATE INDEX IF NOT EXISTS ix_hangfire_set_key_score ON "set" (key, score);



CREATE TABLE aggregatedcounter (
    "id" uuid    default gen_random_uuid() PRIMARY KEY NOT NULL,
    "key" text NOT NULL UNIQUE,
    "value" int8 NOT NULL,
    "expireat" timestamptz
);

 

RESET search_path;