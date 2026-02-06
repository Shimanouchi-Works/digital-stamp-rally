CREATE TABLE IF NOT EXISTS goals (
  id BIGINT(12) NOT NULL,
  events_id BIGINT(12) NOT NULL,
  participant_sessions_id BIGINT(12) NOT NULL,
  goaled_at DATETIME NULL,
  achievement_code CHAR(8) NOT NULL,
  created_at DATETIME NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_goals_event_session (events_id, participant_sessions_id),
  INDEX idx_goals_event_goaled_at (events_id, goaled_at),
  CONSTRAINT fk_goals_events FOREIGN KEY (events_id) REFERENCES events(id),
  CONSTRAINT fk_goals_sessions
    FOREIGN KEY (participant_sessions_id, events_id)
    REFERENCES participant_sessions(id, events_id)
) ENGINE=InnoDB;