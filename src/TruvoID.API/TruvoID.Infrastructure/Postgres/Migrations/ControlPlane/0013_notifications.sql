-- In-app notifications for signed-in users (workspace members and platform admins).
CREATE TABLE control.notification (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES control.app_user (id),
    organization_id uuid REFERENCES control.organization (id),
    kind            text NOT NULL,
    title           text NOT NULL,
    body            text,
    link            text,
    read_at         timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX notification_user_idx ON control.notification (user_id, created_at DESC);
CREATE INDEX notification_unread_idx ON control.notification (user_id) WHERE read_at IS NULL;

GRANT SELECT, INSERT ON control.notification TO {{app_role}};
GRANT UPDATE (read_at) ON control.notification TO {{app_role}};
