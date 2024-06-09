INSERT INTO "userAccess"."AspNetRoles"
    ( "Id"
    , "Name"
    , "NormalizedName"
    , "ConcurrencyStamp"
    )
VALUES
    ( gen_random_uuid()
    , 'admin'
    , 'ADMIN'
    , gen_random_uuid()
    )
