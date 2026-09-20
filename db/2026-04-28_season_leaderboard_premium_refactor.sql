BEGIN;

-- ---------------------------------------------------------------------------
-- Season tables: create missing structures first so older databases can migrate.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.game_seasons
(
    game_seasons_id UUID PRIMARY KEY DEFAULT uuidv7(),
    season_number INT NOT NULL UNIQUE,
    status VARCHAR(32) NOT NULL DEFAULT 'active',
    starts_at_utc TIMESTAMP NOT NULL,
    ends_at_utc TIMESTAMP NOT NULL,
    closed_at_utc TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS public.game_season_leaderboard_entries
(
    game_season_leaderboard_entries_id UUID PRIMARY KEY DEFAULT uuidv7(),
    game_seasons_id UUID NOT NULL,
    entry_kind VARCHAR(32) NOT NULL,
    position INT NOT NULL,
    players_id UUID NULL,
    countries_id UUID NULL,
    entry_name VARCHAR(250) NOT NULL,
    season_score DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_money DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_oil DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_uranium DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_chips DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_money DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_oil DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_uranium DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_chips DECIMAL(18, 2) NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_position UNIQUE (game_seasons_id, entry_kind, position),
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_player UNIQUE (game_seasons_id, entry_kind, players_id),
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_country UNIQUE (game_seasons_id, entry_kind, countries_id)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'game_seasons'
    ) THEN
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name = 'game_season_leaderboard_entries'
              AND constraint_name = 'game_season_leaderboard_entries_game_seasons_id_fkey'
        ) THEN
            ALTER TABLE public.game_season_leaderboard_entries
                ADD CONSTRAINT game_season_leaderboard_entries_game_seasons_id_fkey
                FOREIGN KEY (game_seasons_id) REFERENCES public.game_seasons (game_seasons_id) ON DELETE CASCADE;
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'players'
    ) THEN
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name = 'game_season_leaderboard_entries'
              AND constraint_name = 'game_season_leaderboard_entries_players_id_fkey'
        ) THEN
            ALTER TABLE public.game_season_leaderboard_entries
                ADD CONSTRAINT game_season_leaderboard_entries_players_id_fkey
                FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE SET NULL;
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'countries'
    ) THEN
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name = 'game_season_leaderboard_entries'
              AND constraint_name = 'game_season_leaderboard_entries_countries_id_fkey'
        ) THEN
            ALTER TABLE public.game_season_leaderboard_entries
                ADD CONSTRAINT game_season_leaderboard_entries_countries_id_fkey
                FOREIGN KEY (countries_id) REFERENCES countries (countries_id) ON DELETE SET NULL;
        END IF;
    END IF;
END$$;

-- ---------------------------------------------------------------------------
-- Players: move premium state from users to players and remove legacy flags.
-- ---------------------------------------------------------------------------

ALTER TABLE IF EXISTS public.players
    ADD COLUMN IF NOT EXISTS battle_pass_valid_till TIMESTAMP NULL;

ALTER TABLE IF EXISTS public.players
    ADD COLUMN IF NOT EXISTS shield_valid_till TIMESTAMP NULL;

ALTER TABLE IF EXISTS public.players
    ADD COLUMN IF NOT EXISTS battle_pass_trial_granted_at TIMESTAMP NULL;

ALTER TABLE IF EXISTS public.players
    DROP COLUMN IF EXISTS premium_shield_enabled;

ALTER TABLE IF EXISTS public.players
    DROP COLUMN IF EXISTS season_score;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'users'
          AND column_name = 'premium_valid_till'
    ) THEN
        EXECUTE '
            UPDATE players p
            SET battle_pass_valid_till = COALESCE(p.battle_pass_valid_till, u.premium_valid_till)
            FROM users u
            WHERE u.users_id = p.users_id
              AND u.premium_valid_till IS NOT NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'users'
          AND column_name = 'battle_pass_valid_till'
    ) THEN
        EXECUTE '
            UPDATE players p
            SET battle_pass_valid_till = COALESCE(p.battle_pass_valid_till, u.battle_pass_valid_till)
            FROM users u
            WHERE u.users_id = p.users_id
              AND u.battle_pass_valid_till IS NOT NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'users'
          AND column_name = 'shield_valid_till'
    ) THEN
        EXECUTE '
            UPDATE players p
            SET shield_valid_till = COALESCE(p.shield_valid_till, u.shield_valid_till)
            FROM users u
            WHERE u.users_id = p.users_id
              AND u.shield_valid_till IS NOT NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'users'
          AND column_name = 'battle_pass_trial_granted_at'
    ) THEN
        EXECUTE '
            UPDATE players p
            SET battle_pass_trial_granted_at = COALESCE(p.battle_pass_trial_granted_at, u.battle_pass_trial_granted_at)
            FROM users u
            WHERE u.users_id = p.users_id
              AND u.battle_pass_trial_granted_at IS NOT NULL';
    END IF;
END$$;

ALTER TABLE IF EXISTS public.users
    DROP COLUMN IF EXISTS premium_valid_till;

ALTER TABLE IF EXISTS public.users
    DROP COLUMN IF EXISTS battle_pass_valid_till;

ALTER TABLE IF EXISTS public.users
    DROP COLUMN IF EXISTS shield_valid_till;

ALTER TABLE IF EXISTS public.users
    DROP COLUMN IF EXISTS battle_pass_trial_granted_at;

-- ---------------------------------------------------------------------------
-- Game seasons: remove duplicated winner columns and keep archive in leaderboard.
-- ---------------------------------------------------------------------------

ALTER TABLE IF EXISTS public.game_seasons
    ADD COLUMN IF NOT EXISTS closed_at_utc TIMESTAMP NULL;

ALTER TABLE IF EXISTS public.game_seasons
    DROP COLUMN IF EXISTS winner_players_id;

ALTER TABLE IF EXISTS public.game_seasons
    DROP COLUMN IF EXISTS winner_countries_id;

ALTER TABLE IF EXISTS public.game_seasons
    DROP COLUMN IF EXISTS winner_player_season_score;

ALTER TABLE IF EXISTS public.game_seasons
    DROP COLUMN IF EXISTS winner_country_season_score;

-- ---------------------------------------------------------------------------
-- Season leaderboard: normalize columns for history entries.
-- ---------------------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'game_season_leaderboard_entries'
          AND column_name = 'lifetime_score'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'game_season_leaderboard_entries'
          AND column_name = 'season_score'
    ) THEN
        EXECUTE 'ALTER TABLE public.game_season_leaderboard_entries RENAME COLUMN lifetime_score TO season_score';
    END IF;
END$$;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS season_score DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS player_money DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS player_oil DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS player_uranium DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS player_chips DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS country_money DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS country_oil DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS country_uranium DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    ADD COLUMN IF NOT EXISTS country_chips DECIMAL(18, 2) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    DROP COLUMN IF EXISTS country_name;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    DROP COLUMN IF EXISTS country_flag;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    DROP COLUMN IF EXISTS country_iso_code2;

ALTER TABLE IF EXISTS public.game_season_leaderboard_entries
    DROP COLUMN IF EXISTS lifetime_score;

CREATE UNIQUE INDEX IF NOT EXISTS game_season_leaderboard_entries_uq_season_kind_position
    ON public.game_season_leaderboard_entries (game_seasons_id, entry_kind, position);

CREATE UNIQUE INDEX IF NOT EXISTS game_season_leaderboard_entries_uq_season_kind_player
    ON public.game_season_leaderboard_entries (game_seasons_id, entry_kind, players_id);

CREATE UNIQUE INDEX IF NOT EXISTS game_season_leaderboard_entries_uq_season_kind_country
    ON public.game_season_leaderboard_entries (game_seasons_id, entry_kind, countries_id);

COMMIT;
