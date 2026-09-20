drop table if exists background_worker_last_runs cascade;
drop table if exists player_trades cascade;
drop table if exists country_wars cascade;
drop table if exists country_buildings cascade;
drop table if exists country_trade_policies cascade;
drop table if exists chat_global_messages cascade;
drop table if exists chat_private_messages cascade;
drop table if exists chat_country_messages cascade;
drop table if exists player_badges cascade;
drop table if exists player_daily_stats cascade;
drop table if exists player_military_production cascade;
drop table if exists player_military_units cascade;
drop table if exists player_battle_report_units cascade;
drop table if exists player_battle_reports cascade;
drop table if exists player_notifications cascade;
drop table if exists premium_payment_events cascade;
drop table if exists user_login_histories cascade;
drop table if exists player_siege_units cascade;
drop table if exists player_sieges cascade;
drop table if exists country_sieges cascade;
drop table if exists player_military_transport_units cascade;
drop table if exists player_military_transports cascade;
drop table if exists player_transports cascade;
drop table if exists players cascade;
drop table if exists users cascade;
drop table if exists countries cascade;

CREATE TABLE background_worker_last_runs
(
    run_type             VARCHAR(128) PRIMARY KEY,
    last_executed_at_utc TIMESTAMP NOT NULL,
    updated_at           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE users
(
    users_id              UUID PRIMARY KEY      DEFAULT uuidv7(),
    email                 VARCHAR(250) NOT NULL UNIQUE,
    password              VARCHAR(250) NOT NULL,
    auth_provider         VARCHAR(32)  NOT NULL DEFAULT 'local',
    google_subject_id     VARCHAR(255) NULL UNIQUE,
    google_email_verified BOOLEAN      NOT NULL DEFAULT FALSE,
    google_linked_at      TIMESTAMP    NULL,
    stripe_customer_id    VARCHAR(255) NULL UNIQUE,
    last_login            TIMESTAMP    NOT NULL,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE user_login_histories
(
    user_login_histories_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    users_id                UUID         NOT NULL,
    is_successful           BOOLEAN      NOT NULL DEFAULT FALSE,
    failure_reason          VARCHAR(250) NULL,
    attempted_at            TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (users_id) REFERENCES users (users_id) ON DELETE SET NULL
);

CREATE TABLE premium_payment_events
(
    premium_payment_events_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    users_id                  UUID         NOT NULL,
    stripe_event_id           VARCHAR(255) NOT NULL UNIQUE,
    event_type                VARCHAR(120) NOT NULL,
    stripe_object_id          VARCHAR(255) NULL,
    status                    VARCHAR(32)  NOT NULL,
    error_message             TEXT         NULL,
    payload_json              TEXT         NOT NULL,
    created_at                TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at              TIMESTAMP    NULL,
    FOREIGN KEY (users_id) REFERENCES users (users_id) ON DELETE SET NULL
);



CREATE TABLE countries
(
    countries_id                               UUID PRIMARY KEY        DEFAULT uuidv7(),
    name                                       VARCHAR(250)   NOT NULL UNIQUE,
    iso_code_2                                 VARCHAR(10)    NULL,
    flag                                       VARCHAR(10)    NULL,
    president_players_id                       UUID           NULL,
    tax_percent                                DECIMAL(5, 2)  NOT NULL DEFAULT 0.00,
    money                                      DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    oil                                        DECIMAL(18, 2) NOT NULL DEFAULT 0,
    uranium                                    DECIMAL(18, 2) NOT NULL DEFAULT 0,
    chips                                      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    oil_tier                                   SMALLINT       NOT NULL DEFAULT 1,
    uranium_tier                               SMALLINT       NOT NULL DEFAULT 1,
    chips_tier                                 SMALLINT       NOT NULL DEFAULT 1,
    color_hex                                  VARCHAR(7)              DEFAULT '#3498db',
    is_allow_to_attack_inside_country          BOOLEAN        NOT NULL DEFAULT FALSE,
    is_allow_to_attack_without_war_declaration BOOLEAN        NOT NULL DEFAULT FALSE
);

CREATE TABLE players
(
    players_id                UUID PRIMARY KEY        DEFAULT uuidv7(),
    countries_id              UUID           NOT NULL,
    users_id                  UUID           NOT NULL,
    name                      VARCHAR(250)   NOT NULL,
    location_x                DECIMAL(18, 8) NOT NULL,
    location_y                DECIMAL(18, 8) NOT NULL,
    money                     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    score                     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    oil                       DECIMAL(18, 2) NOT NULL DEFAULT 0,
    uranium                   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    chips                     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    oil_tier                  SMALLINT       NOT NULL DEFAULT 1,
    uranium_tier              SMALLINT       NOT NULL DEFAULT 1,
    chips_tier                SMALLINT       NOT NULL DEFAULT 1,
    player_avatar             VARCHAR(500)   NULL,
    has_relocated_region      BOOLEAN        NOT NULL DEFAULT FALSE,
    battle_pass_valid_till    TIMESTAMP      NULL,
    shield_valid_till         TIMESTAMP      NULL,
    battle_pass_trial_granted_at TIMESTAMP   NULL,
    is_npc                    BOOLEAN        NOT NULL DEFAULT FALSE,
    is_country_council_member BOOLEAN        NOT NULL DEFAULT FALSE,
    FOREIGN KEY (users_id) REFERENCES users (users_id) ON DELETE CASCADE,
    FOREIGN KEY (countries_id) REFERENCES countries (countries_id) ON DELETE SET NULL,
    CONSTRAINT players_unique_user_id UNIQUE (users_id)
);

CREATE TABLE game_seasons
(
    game_seasons_id            UUID PRIMARY KEY      DEFAULT uuidv7(),
    season_number              INT           NOT NULL UNIQUE,
    status                     VARCHAR(32)   NOT NULL DEFAULT 'active',
    starts_at_utc              TIMESTAMP     NOT NULL,
    ends_at_utc                TIMESTAMP     NOT NULL,
    closed_at_utc              TIMESTAMP     NULL,
    created_at                 TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at                 TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE game_season_leaderboard_entries
(
    game_season_leaderboard_entries_id UUID PRIMARY KEY DEFAULT uuidv7(),
    game_seasons_id                   UUID         NOT NULL REFERENCES game_seasons (game_seasons_id) ON DELETE CASCADE,
    entry_kind                        VARCHAR(32)  NOT NULL,
    position                          INT          NOT NULL,
    players_id                        UUID         NULL REFERENCES players (players_id) ON DELETE SET NULL,
    countries_id                      UUID         NULL REFERENCES countries (countries_id) ON DELETE SET NULL,
    entry_name                        VARCHAR(250) NOT NULL,
    season_score                      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_money                      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_oil                        DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_uranium                    DECIMAL(18, 2) NOT NULL DEFAULT 0,
    player_chips                      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_money                     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_oil                       DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_uranium                   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    country_chips                     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    created_at                        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_position UNIQUE (game_seasons_id, entry_kind, position),
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_player UNIQUE (game_seasons_id, entry_kind, players_id),
    CONSTRAINT game_season_leaderboard_entries_uq_season_kind_country UNIQUE (game_seasons_id, entry_kind, countries_id)
);

CREATE TABLE player_trades
(
    player_trades_id UUID PRIMARY KEY DEFAULT uuidv7(),
    from_players_id  UUID           NOT NULL,
    to_players_id    UUID           NULL,
    resource_code    VARCHAR(16)    NOT NULL,
    quantity         DECIMAL(18, 2) NOT NULL,
    price_per_unit   DECIMAL(18, 2) NOT NULL,
    total_price      DECIMAL(18, 2) NOT NULL,
    status           VARCHAR(250)   NOT NULL,
    country_tax_paid DECIMAL(18, 2)   DEFAULT 0.00,
    created_at       TIMESTAMP        DEFAULT CURRENT_TIMESTAMP,
    transport_start_time       TIMESTAMP      NOT NULL,
    transport_end_time         TIMESTAMP      NOT NULL,
    FOREIGN KEY (from_players_id) REFERENCES players (players_id),
    FOREIGN KEY (to_players_id) REFERENCES players (players_id)
);

CREATE TABLE country_buildings
(
    country_buildings_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    countries_id         UUID         NOT NULL,
    building_code        VARCHAR(250) NOT NULL,
    level                INT          NOT NULL DEFAULT 1,
    status               VARCHAR(32)  NOT NULL DEFAULT 'hold',
    started_at           TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at             TIMESTAMP    NULL,
    FOREIGN KEY (countries_id) REFERENCES countries (countries_id) ON DELETE CASCADE
);

CREATE TABLE country_wars
(
    country_wars_id UUID PRIMARY KEY     DEFAULT uuidv7(),
    country_from_id UUID        NOT NULL,
    country_to_id   UUID        NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'active',
    started_at      TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at        TIMESTAMP   NULL,
    UNIQUE (country_from_id, country_to_id),
    FOREIGN KEY (country_from_id) REFERENCES countries (countries_id) ON DELETE CASCADE,
    FOREIGN KEY (country_to_id) REFERENCES countries (countries_id) ON DELETE CASCADE
);

CREATE TABLE country_trade_policies
(
    country_trade_policies_id UUID PRIMARY KEY       DEFAULT uuidv7(),
    source_countries_id       UUID          NOT NULL REFERENCES countries (countries_id) ON DELETE CASCADE,
    target_countries_id       UUID          NOT NULL REFERENCES countries (countries_id) ON DELETE CASCADE,
    is_embargo                BOOLEAN       NOT NULL DEFAULT FALSE,
    tariff_percent            DECIMAL(5, 2) NOT NULL DEFAULT 0.00,
    CONSTRAINT country_trade_policies_uq_pair UNIQUE (source_countries_id, target_countries_id)
);

CREATE TABLE player_badges
(
    player_badges_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    players_id       UUID         NOT NULL,
    badge_code       VARCHAR(250) NOT NULL,
    awarded_at       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_daily_stats
(
    player_daily_stats_id UUID PRIMARY KEY        DEFAULT uuidv7(),
    players_id            UUID           NOT NULL,
    stats_date            DATE           NOT NULL,
    money                 DECIMAL(18, 2) NOT NULL,
    score                 DECIMAL(18, 2) NOT NULL DEFAULT 0,
    oil                   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    uranium               DECIMAL(18, 2) NOT NULL DEFAULT 0,
    chips                 DECIMAL(18, 2) NOT NULL DEFAULT 0,
    army_total            INT            NOT NULL,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE,
    CONSTRAINT player_daily_stats_uq_player_daily_stats_player_date UNIQUE (players_id, stats_date)
);


CREATE TABLE player_military_production
(
    player_military_orders_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    players_id                UUID         NOT NULL,
    military_units_code       VARCHAR(250) NOT NULL,
    quantity                  INT          NOT NULL,
    level                     INT          NOT NULL DEFAULT 1,
    produced_quantity         INT          NOT NULL DEFAULT 0,
    created_at                TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_military_units
(
    player_military_units_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    players_id               UUID         NOT NULL,
    military_units_code      VARCHAR(250) NOT NULL,
    level                    INT          NOT NULL DEFAULT 1,
    quantity                 INT          NOT NULL DEFAULT 0,
    created_at               TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_battle_reports
(
    player_battle_reports_id UUID PRIMARY KEY        DEFAULT uuidv7(),
    player_from_id           UUID           NOT NULL,
    player_to_id             UUID           NOT NULL,
    result                   VARCHAR(250)   NOT NULL,
    attacker_power           INT            NOT NULL DEFAULT 0,
    defender_power           INT            NOT NULL DEFAULT 0,
    attacker_losses          INT            NOT NULL DEFAULT 0,
    defender_losses          INT            NOT NULL DEFAULT 0,
    loot_money               DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_oil                 DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_uranium             DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_chips               DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    started_at               TIMESTAMP      NOT NULL,
    ended_at                 TIMESTAMP      NOT NULL,
    created_at               TIMESTAMP               DEFAULT CURRENT_TIMESTAMP,
    is_archived              BOOLEAN        NOT NULL DEFAULT FALSE,
    archived_at              TIMESTAMP      NULL,
    FOREIGN KEY (player_from_id) REFERENCES players (players_id) ON DELETE CASCADE,
    FOREIGN KEY (player_to_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_battle_report_units
(
    player_battle_report_units_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    player_battle_reports_id      UUID         NOT NULL,
    side                          VARCHAR(32)  NOT NULL,
    military_units_code           VARCHAR(250) NOT NULL,
    level                         INT          NOT NULL DEFAULT 1,
    starting_quantity             INT          NOT NULL DEFAULT 0,
    remaining_quantity            INT          NOT NULL DEFAULT 0,
    lost_quantity                 INT          NOT NULL DEFAULT 0,
    created_at                    TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (player_battle_reports_id) REFERENCES player_battle_reports (player_battle_reports_id) ON DELETE CASCADE
);

CREATE TABLE player_notifications
(
    player_notifications_id UUID PRIMARY KEY     DEFAULT uuidv7(),
    players_id              UUID        NOT NULL,
    type                    VARCHAR(64) NOT NULL,
    data_json               TEXT        NULL,
    is_read                 BOOLEAN     NOT NULL DEFAULT FALSE,
    is_popup_delivered      BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at              TIMESTAMP            DEFAULT CURRENT_TIMESTAMP,
    read_at                 TIMESTAMP   NULL,
    popup_delivered_at      TIMESTAMP   NULL,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_sieges
(
    player_sieges_id UUID PRIMARY KEY        DEFAULT uuidv7(),
    player_from_id   UUID           NOT NULL,
    player_to_id     UUID           NOT NULL,
    status           VARCHAR(250)   NOT NULL,
    loot_money       DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_oil         DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_uranium     DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    loot_chips       DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    started_at       TIMESTAMP      NOT NULL,
    ended_at         TIMESTAMP      NULL,
    last_tick_at     TIMESTAMP      NULL,
    is_archived      BOOLEAN        NOT NULL DEFAULT FALSE,
    archived_at      TIMESTAMP      NULL,
    FOREIGN KEY (player_from_id) REFERENCES players (players_id) ON DELETE CASCADE,
    FOREIGN KEY (player_to_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_siege_units
(
    player_siege_units_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    player_sieges_id      UUID         NOT NULL,
    side                  VARCHAR(32)  NOT NULL,
    military_units_code   VARCHAR(250) NOT NULL,
    level                 INT          NOT NULL DEFAULT 1,
    starting_quantity     INT          NOT NULL DEFAULT 0,
    current_quantity      INT          NOT NULL DEFAULT 0,
    lost_quantity         INT          NOT NULL DEFAULT 0,
    created_at            TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    updated_at            TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (player_sieges_id) REFERENCES player_sieges (player_sieges_id) ON DELETE CASCADE
);

CREATE TABLE player_military_transports
(
    player_military_transports_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    player_from_id                UUID         NOT NULL,
    player_to_id                  UUID         NOT NULL,
    mission_type                  VARCHAR(250) NOT NULL,
    status                        VARCHAR(250) NOT NULL,
    start_time                    TIMESTAMP    NOT NULL,
    end_time                      TIMESTAMP    NOT NULL,
    created_at                    TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    auto_return_after_battle      BOOLEAN      NOT NULL DEFAULT FALSE,
    is_archived                   BOOLEAN      NOT NULL DEFAULT FALSE,
    archived_at                   TIMESTAMP    NULL,
    FOREIGN KEY (player_from_id) REFERENCES players (players_id) ON DELETE CASCADE,
    FOREIGN KEY (player_to_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE player_military_transport_units
(
    player_military_transport_units_id UUID PRIMARY KEY      DEFAULT uuidv7(),
    player_military_transports_id      UUID         NOT NULL,
    military_units_code                VARCHAR(250) NOT NULL,
    level                              INT          NOT NULL DEFAULT 1,
    quantity                           INT          NOT NULL DEFAULT 0,
    FOREIGN KEY (player_military_transports_id) REFERENCES player_military_transports (player_military_transports_id) ON DELETE CASCADE
);



CREATE TABLE chat_global_messages
(
    message_id   UUID PRIMARY KEY DEFAULT uuidv7(),
    players_id   UUID NOT NULL,
    message_text TEXT NOT NULL,
    sent_at      TIMESTAMP        DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE chat_private_messages
(
    message_id          UUID PRIMARY KEY DEFAULT uuidv7(),
    sender_players_id   UUID NOT NULL,
    receiver_players_id UUID NOT NULL,
    message_text        TEXT NOT NULL,
    is_read             BOOLEAN          DEFAULT FALSE,
    sent_at             TIMESTAMP        DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (sender_players_id) REFERENCES players (players_id) ON DELETE CASCADE,
    FOREIGN KEY (receiver_players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

CREATE TABLE chat_country_messages
(
    message_id   UUID PRIMARY KEY DEFAULT uuidv7(),
    countries_id UUID NOT NULL,
    players_id   UUID NOT NULL,
    message_text TEXT NOT NULL,
    sent_at      TIMESTAMP        DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (countries_id) REFERENCES countries (countries_id) ON DELETE CASCADE,
    FOREIGN KEY (players_id) REFERENCES players (players_id) ON DELETE CASCADE
);

INSERT INTO countries (name, iso_code_2, flag, color_hex)
VALUES ('Afghanistan', 'AF', 'af', '#06fa56'),
       ('Albania', 'AL', 'al', '#e892e7'),
       ('Algeria', 'DZ', 'dz', '#fa3c7f'),
       ('American Samoa', 'AS', 'as', '#a2c291'),
       ('Andorra', 'AD', 'ad', '#e182eb'),
       ('Angola', 'AO', 'ao', '#2c64c5'),
       ('Anguilla', 'AI', 'ai', '#0a40e3'),
       ('Antarctica', 'AQ', 'aq', '#406a82'),
       ('Antigua and Barb.', 'AG', 'ag', '#6b712d'),
       ('Argentina', 'AR', 'ar', '#5b61a1'),
       ('Armenia', 'AM', 'am', '#25ec91'),
       ('Aruba', 'AW', 'aw', '#1e5342'),
       ('Australia', 'AU', 'au', '#6cc98b'),
       ('Austria', 'AT', 'at', '#fa8684'),
       ('Azerbaijan', 'AZ', 'az', '#546a9a'),
       ('Bahamas', 'BS', 'bs', '#9a231c'),
       ('Bahrain', 'BH', 'bh', '#1baa5a'),
       ('Bangladesh', 'BD', 'bd', '#87a475'),
       ('Barbados', 'BB', 'bb', '#9d3d90'),
       ('Belarus', 'BY', 'by', '#925ab3'),
       ('Belgium', 'BE', 'be', '#d3dcf4'),
       ('Belize', 'BZ', 'bz', '#e45fbc'),
       ('Benin', 'BJ', 'bj', '#ddd70b'),
       ('Bermuda', 'BM', 'bm', '#5089fa'),
       ('Bhutan', 'BT', 'bt', '#277b18'),
       ('Bolivia', 'BO', 'bo', '#7b60a3'),
       ('Bosnia and Herz.', 'BA', 'ba', '#5fc810'),
       ('Botswana', 'BW', 'bw', '#81043f'),
       ('Br. Indian Ocean Ter.', 'IO', 'io', '#cf3882'),
       ('Brazil', 'BR', 'br', '#19d332'),
       ('British Virgin Is.', 'VG', 'vg', '#4d6d91'),
       ('Brunei', 'BN', 'bn', '#fc37fb'),
       ('Bulgaria', 'BG', 'bg', '#461b19'),
       ('Burkina Faso', 'BF', 'bf', '#7b8d2f'),
       ('Burundi', 'BI', 'bi', '#af7f02'),
       ('Cabo Verde', 'CV', 'cv', '#a5c8c3'),
       ('Cambodia', 'KH', 'kh', '#fd7030'),
       ('Cameroon', 'CM', 'cm', '#707354'),
       ('Canada', 'CA', 'ca', '#3e8d11'),
       ('Cayman Is.', 'KY', 'ky', '#b8ea98'),
       ('Central African Rep.', 'CF', 'cf', '#758951'),
       ('Chad', 'TD', 'td', '#fc0894'),
       ('Chile', 'CL', 'cl', '#5bc574'),
       ('China', 'CN', 'cn', '#1c2903'),
       ('Colombia', 'CO', 'co', '#42983b'),
       ('Comoros', 'KM', 'km', '#dd480f'),
       ('Congo', 'CG', 'cg', '#5202c6'),
       ('Cook Is.', 'CK', 'ck', '#534ac7'),
       ('Costa Rica', 'CR', 'cr', '#1d7b33'),
       ('Croatia', 'HR', 'hr', '#fd4c63'),
       ('Cuba', 'CU', 'cu', '#c83f07'),
       ('Curaçao', 'CW', 'cw', '#9af310'),
       ('Cyprus', 'CY', 'cy', '#9025a1'),
       ('Czechia', 'CZ', 'cz', '#928568'),
       ('Côte d''Ivoire', 'CI', 'ci', '#3ba0f4'),
       ('Dem. Rep. Congo', 'CD', 'cd', '#4170ac'),
       ('Denmark', 'DK', 'dk', '#dd65ef'),
       ('Djibouti', 'DJ', 'dj', '#27a577'),
       ('Dominica', 'DM', 'dm', '#2ecda7'),
       ('Dominican Rep.', 'DO', 'do', '#c23fa9'),
       ('Ecuador', 'EC', 'ec', '#3fd6b6'),
       ('Egypt', 'EG', 'eg', '#fbe463'),
       ('El Salvador', 'SV', 'sv', '#d38212'),
       ('Eq. Guinea', 'GQ', 'gq', '#8d36b3'),
       ('Eritrea', 'ER', 'er', '#1bd3a0'),
       ('Estonia', 'EE', 'ee', '#a57b84'),
       ('Ethiopia', 'ET', 'et', '#ae41a6'),
       ('Faeroe Is.', 'FO', 'fo', '#c0398f'),
       ('Falkland Is.', 'FK', 'fk', '#8c8ba2'),
       ('Fiji', 'FJ', 'fj', '#a0137b'),
       ('Finland', 'FI', 'fi', '#f0aa03'),
       ('Fr. Polynesia', 'PF', 'pf', '#210809'),
       ('Fr. S. Antarctic Lands', 'TF', 'tf', '#c3ee2a'),
       ('Gabon', 'GA', 'ga', '#cd9573'),
       ('Gambia', 'GM', 'gm', '#64f3bd'),
       ('Georgia', 'GE', 'ge', '#f80372'),
       ('Germany', 'DE', 'de', '#3a52f3'),
       ('Ghana', 'GH', 'gh', '#6848ae'),
       ('Gibraltar', 'GI', 'gi', '#02c73f'),
       ('Greece', 'GR', 'gr', '#f214a7'),
       ('Greenland', 'GL', 'gl', '#ad7093'),
       ('Grenada', 'GD', 'gd', '#56a036'),
       ('Guam', 'GU', 'gu', '#30531a'),
       ('Guatemala', 'GT', 'gt', '#cd6a9b'),
       ('Guernsey', 'GG', 'gg', '#86d8d9'),
       ('Guinea', 'GN', 'gn', '#accb66'),
       ('Guinea-Bissau', 'GW', 'gw', '#c17d19'),
       ('Guyana', 'GY', 'gy', '#1daf9c'),
       ('Haiti', 'HT', 'ht', '#90d64e'),
       ('Heard I. and McDonald Is.', 'HM', 'hm', '#36a12a'),
       ('Honduras', 'HN', 'hn', '#ac4a27'),
       ('Hong Kong', 'HK', 'hk', '#69e1aa'),
       ('Hungary', 'HU', 'hu', '#35b528'),
       ('Iceland', 'IS', 'is', '#0bfc16'),
       ('India', 'IN', 'in', '#c86ee0'),
       ('Indonesia', 'ID', 'id', '#b718ad'),
       ('Iran', 'IR', 'ir', '#4f74d3'),
       ('Iraq', 'IQ', 'iq', '#560e51'),
       ('Ireland', 'IE', 'ie', '#d2cb7b'),
       ('Isle of Man', 'IM', 'im', '#7c78eb'),
       ('Israel', 'IL', 'il', '#88588b'),
       ('Italy', 'IT', 'it', '#cd3210'),
       ('Jamaica', 'JM', 'jm', '#52c570'),
       ('Japan', 'JP', 'jp', '#24d22e'),
       ('Jersey', 'JE', 'je', '#069b38'),
       ('Jordan', 'JO', 'jo', '#60aea2'),
       ('Kazakhstan', 'KZ', 'kz', '#4aceb7'),
       ('Kenya', 'KE', 'ke', '#518f4a'),
       ('Kiribati', 'KI', 'ki', '#609caf'),
       ('Kuwait', 'KW', 'kw', '#cd37b8'),
       ('Kyrgyzstan', 'KG', 'kg', '#56d721'),
       ('Laos', 'LA', 'la', '#6b40fe'),
       ('Latvia', 'LV', 'lv', '#a7bdee'),
       ('Lebanon', 'LB', 'lb', '#c95127'),
       ('Lesotho', 'LS', 'ls', '#e82809'),
       ('Liberia', 'LR', 'lr', '#90a7c4'),
       ('Libya', 'LY', 'ly', '#c2d757'),
       ('Liechtenstein', 'LI', 'li', '#14efbb'),
       ('Lithuania', 'LT', 'lt', '#c56260'),
       ('Luxembourg', 'LU', 'lu', '#920d0c'),
       ('Macao', 'MO', 'mo', '#eb0459'),
       ('Madagascar', 'MG', 'mg', '#ba2a03'),
       ('Malawi', 'MW', 'mw', '#f9f315'),
       ('Malaysia', 'MY', 'my', '#75dfae'),
       ('Maldives', 'MV', 'mv', '#77d96f'),
       ('Mali', 'ML', 'ml', '#d01fd9'),
       ('Malta', 'MT', 'mt', '#08ad08'),
       ('Marshall Is.', 'MH', 'mh', '#002f27'),
       ('Mauritania', 'MR', 'mr', '#d5c442'),
       ('Mauritius', 'MU', 'mu', '#e5919b'),
       ('Mexico', 'MX', 'mx', '#0b9872'),
       ('Micronesia', 'FM', 'fm', '#ff94b9'),
       ('Moldova', 'MD', 'md', '#7dc10e'),
       ('Monaco', 'MC', 'mc', '#92a54b'),
       ('Mongolia', 'MN', 'mn', '#943afa'),
       ('Montenegro', 'ME', 'me', '#9ee9d8'),
       ('Montserrat', 'MS', 'ms', '#7a663c'),
       ('Morocco', 'MA', 'ma', '#2a6039'),
       ('Mozambique', 'MZ', 'mz', '#ac6ad5'),
       ('Myanmar', 'MM', 'mm', '#ad05f7'),
       ('N. Mariana Is.', 'MP', 'mp', '#c90a91'),
       ('Namibia', 'NA', 'na', '#d4cd0d'),
       ('Nauru', 'NR', 'nr', '#c3938d'),
       ('Nepal', 'NP', 'np', '#8bc2af'),
       ('Netherlands', 'NL', 'nl', '#796834'),
       ('New Caledonia', 'NC', 'nc', '#90581d'),
       ('New Zealand', 'NZ', 'nz', '#8e3eb2'),
       ('Nicaragua', 'NI', 'ni', '#fff6fa'),
       ('Niger', 'NE', 'ne', '#dc3306'),
       ('Nigeria', 'NG', 'ng', '#bf7410'),
       ('Niue', 'NU', 'nu', '#9393cb'),
       ('Norfolk Island', 'NF', 'nf', '#227dc2'),
       ('North Korea', 'KP', 'kp', '#da2be3'),
       ('North Macedonia', 'MK', 'mk', '#fbd1e7'),
       ('Oman', 'OM', 'om', '#bfbebc'),
       ('Pakistan', 'PK', 'pk', '#d71bdd'),
       ('Palau', 'PW', 'pw', '#6e029f'),
       ('Palestine', 'PS', 'ps', '#d3d4c5'),
       ('Panama', 'PA', 'pa', '#06f6a4'),
       ('Papua New Guinea', 'PG', 'pg', '#49f38f'),
       ('Paraguay', 'PY', 'py', '#0c145a'),
       ('Peru', 'PE', 'pe', '#3acf83'),
       ('Philippines', 'PH', 'ph', '#a25496'),
       ('Pitcairn Is.', 'PN', 'pn', '#7b6166'),
       ('Poland', 'PL', 'pl', '#9b7d17'),
       ('Portugal', 'PT', 'pt', '#35357b'),
       ('Puerto Rico', 'PR', 'pr', '#0fe75a'),
       ('Qatar', 'QA', 'qa', '#0ab687'),
       ('Romania', 'RO', 'ro', '#f5b15f'),
       ('Russia', 'RU', 'ru', '#f9308c'),
       ('Rwanda', 'RW', 'rw', '#5c6dc3'),
       ('S. Geo. and the Is.', 'GS', 'gs', '#71a75a'),
       ('S. Sudan', 'SS', 'ss', '#d53aeb'),
       ('Saint Helena', 'SH', 'sh', '#ec5704'),
       ('Saint Lucia', 'LC', 'lc', '#6907b8'),
       ('Samoa', 'WS', 'ws', '#54df3b'),
       ('San Marino', 'SM', 'sm', '#4e0d4f'),
       ('Saudi Arabia', 'SA', 'sa', '#3dd6b9'),
       ('Senegal', 'SN', 'sn', '#926665'),
       ('Serbia', 'RS', 'rs', '#8cee50'),
       ('Seychelles', 'SC', 'sc', '#6a65ed'),
       ('Sierra Leone', 'SL', 'sl', '#74b8d5'),
       ('Singapore', 'SG', 'sg', '#0f1773'),
       ('Sint Maarten', 'SX', 'sx', '#c5c6b3'),
       ('Slovakia', 'SK', 'sk', '#13dd62'),
       ('Slovenia', 'SI', 'si', '#ce774d'),
       ('Solomon Is.', 'SB', 'sb', '#a06b33'),
       ('Somalia', 'SO', 'so', '#98d036'),
       ('South Africa', 'ZA', 'za', '#68caa8'),
       ('South Korea', 'KR', 'kr', '#38dd81'),
       ('Spain', 'ES', 'es', '#04c19f'),
       ('Sri Lanka', 'LK', 'lk', '#2c16b1'),
       ('St-Barthélemy', 'BL', 'bl', '#a6f535'),
       ('St-Martin', 'MF', 'mf', '#12c578'),
       ('St. Kitts and Nevis', 'KN', 'kn', '#aa2b36'),
       ('St. Pierre and Miquelon', 'PM', 'pm', '#21b7eb'),
       ('St. Vin. and Gren.', 'VC', 'vc', '#b7128a'),
       ('Sudan', 'SD', 'sd', '#38f99a'),
       ('Suriname', 'SR', 'sr', '#8cb201'),
       ('Sweden', 'SE', 'se', '#f003c4'),
       ('Switzerland', 'CH', 'ch', '#1ee0bf'),
       ('Syria', 'SY', 'sy', '#174fe2'),
       ('São Tomé and Principe', 'ST', 'st', '#ec8e57'),
       ('Taiwan', 'TW', 'tw', '#5f93f9'),
       ('Tajikistan', 'TJ', 'tj', '#6e4f62'),
       ('Tanzania', 'TZ', 'tz', '#13c499'),
       ('Thailand', 'TH', 'th', '#5b79c4'),
       ('Timor-Leste', 'TL', 'tl', '#c4534c'),
       ('Togo', 'TG', 'tg', '#2de960'),
       ('Tonga', 'TO', 'to', '#304917'),
       ('Trinidad and Tobago', 'TT', 'tt', '#df1f3e'),
       ('Tunisia', 'TN', 'tn', '#947d7d'),
       ('Turkey', 'TR', 'tr', '#ebe021'),
       ('Turkmenistan', 'TM', 'tm', '#ac7a4f'),
       ('Turks and Caicos Is.', 'TC', 'tc', '#ff9c07'),
       ('Tuvalu', 'TV', 'tv', '#271ddf'),
       ('U.S. Minor Outlying Is.', 'UM', 'um', '#40a71d'),
       ('U.S. Virgin Is.', 'VI', 'vi', '#893015'),
       ('Uganda', 'UG', 'ug', '#1db208'),
       ('Ukraine', 'UA', 'ua', '#3943d8'),
       ('United Arab Emirates', 'AE', 'ae', '#ea8a1a'),
       ('United Kingdom', 'GB', 'gb', '#79cba1'),
       ('United States of America', 'US', 'us', '#7516fd'),
       ('Uruguay', 'UY', 'uy', '#9f72f0'),
       ('Uzbekistan', 'UZ', 'uz', '#195dc2'),
       ('Vanuatu', 'VU', 'vu', '#bce87c'),
       ('Vatican', 'VA', 'va', '#3a1112'),
       ('Venezuela', 'VE', 've', '#a99877'),
       ('Vietnam', 'VN', 'vn', '#e14da6'),
       ('W. Sahara', 'EH', 'eh', '#088a20'),
       ('Wallis and Futuna Is.', 'WF', 'wf', '#94e067'),
       ('Yemen', 'YE', 'ye', '#63cf9a'),
       ('Zambia', 'ZM', 'zm', '#70f73e'),
       ('Zimbabwe', 'ZW', 'zw', '#316459'),
       ('eSwatini', 'SZ', 'sz', '#715f9a'),
       ('Åland', 'AX', 'ax', '#58e69a'),
       ('France', 'FR', 'fr', '#174fe2'),
       ('Norway', 'NO', 'no', '#ec8e57'),
       ('Kosovo', 'XK', 'xk', '#5f93f9')
ON CONFLICT (name) DO UPDATE
    SET iso_code_2 = EXCLUDED.iso_code_2,
        flag       = EXCLUDED.flag,
        color_hex  = EXCLUDED.color_hex;

