CREATE TABLE IF NOT EXISTS user_product_sources (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  source_name VARCHAR(120) NOT NULL,
  source_type VARCHAR(50) NOT NULL,
  base_url VARCHAR(1024) NOT NULL,
  search_endpoint VARCHAR(512) NOT NULL,
  query_param_name VARCHAR(80) NOT NULL,
  http_method VARCHAR(10) NOT NULL,
  api_key_encrypted TEXT NOT NULL DEFAULT '',
  headers_json TEXT NOT NULL DEFAULT '{}',
  name_path VARCHAR(512) NOT NULL,
  price_path VARCHAR(512) NOT NULL,
  image_path VARCHAR(512) NOT NULL,
  product_url_path VARCHAR(512) NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT fk_user_product_sources_user FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_user_product_sources_user_is_active
  ON user_product_sources(user_id, is_active);
