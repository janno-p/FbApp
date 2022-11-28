namespace FbApp.Api

open System


type FixtureDto = {
    FixtureId: int64
    CompetitionId: int64
    HomeTeamId: int64 option
    AwayTeamId: int64 option
    UtcDate: DateTimeOffset
    Stage: string
    Status: string
    FullTime: (int * int) option
    HalfTime: (int * int) option
    ExtraTime: (int * int) option
    Penalties: (int * int) option
    Winner: string option
    Duration: string
    }


type FixturesUpdatedIntegrationEvent = {
    Fixtures: FixtureDto[]
    }
