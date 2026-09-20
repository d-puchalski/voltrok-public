BEGIN;

UPDATE players
SET score = ROUND(GREATEST(score, 0) / 20.0, 2);

UPDATE player_daily_stats
SET score = ROUND(GREATEST(score, 0) / 20.0, 2);

COMMIT;
