CREATE TABLE notification_preferences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id UUID NOT NULL REFERENCES institutions(id) ON DELETE CASCADE,
    alert_threshold NUMERIC(18,2) NOT NULL DEFAULT 10000,
    email_alerts_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    sms_alerts_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    verify_email_results BOOLEAN NOT NULL DEFAULT FALSE,
    billing_contact_name VARCHAR(256),
    billing_contact_email VARCHAR(256),
    last_low_balance_alert_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uq_notification_preferences_institution UNIQUE (institution_id)
);

CREATE INDEX idx_notification_preferences_institution_id ON notification_preferences(institution_id);
