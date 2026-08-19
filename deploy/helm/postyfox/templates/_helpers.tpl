{{- define "postyfox.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{ .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "postyfox.labels" -}}
app.kubernetes.io/part-of: postyfox
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end -}}

{{- define "postyfox.image" -}}
{{- $svc := index . 1 -}}
{{- with (index . 0) -}}
{{ .Values.image.registry }}/{{ .Values.image.repository }}-{{ $svc }}:{{ .Values.image.tag | default .Chart.AppVersion }}
{{- end -}}
{{- end -}}

{{- define "postyfox.secretName" -}}
{{ include "postyfox.fullname" . }}-secrets
{{- end -}}

{{- define "postyfox.configName" -}}
{{ include "postyfox.fullname" . }}-config
{{- end -}}

{{/* Postgres connection string: internal chart-managed instance, or the user-supplied external one. */}}
{{- define "postyfox.postgresConnection" -}}
{{- if .Values.postgres.enabled -}}
Host={{ include "postyfox.fullname" . }}-postgres;Port=5432;Database={{ .Values.postgres.database }};Username={{ .Values.postgres.username }};Password={{ .Values.postgres.password }}
{{- else -}}
{{ .Values.secrets.externalPostgresConnection }}
{{- end -}}
{{- end -}}

{{/* RabbitMQ host: internal chart-managed instance, or the user-supplied external one. */}}
{{- define "postyfox.rabbitMqHost" -}}
{{- if .Values.rabbitmq.enabled -}}
{{ include "postyfox.fullname" . }}-rabbitmq
{{- else -}}
{{ .Values.secrets.externalRabbitMqHost }}
{{- end -}}
{{- end -}}

{{- define "postyfox.rabbitMqUser" -}}
{{- if .Values.rabbitmq.enabled -}}
{{ .Values.rabbitmq.username }}
{{- else -}}
{{ .Values.secrets.externalRabbitMqUser }}
{{- end -}}
{{- end -}}

{{- define "postyfox.rabbitMqPassword" -}}
{{- if .Values.rabbitmq.enabled -}}
{{ .Values.rabbitmq.password }}
{{- else -}}
{{ .Values.secrets.externalRabbitMqPassword }}
{{- end -}}
{{- end -}}

{{/* Vault address the apps authenticate against: internal chart-managed instance, or the
     user's own Vault address (config.hashiCorpVaultAddress override), when disabled. */}}
{{- define "postyfox.vaultAddress" -}}
{{- if .Values.vault.enabled -}}
http://{{ include "postyfox.fullname" . }}-vault:8200
{{- else -}}
{{ .Values.config.hashiCorpVaultAddress }}
{{- end -}}
{{- end -}}

{{/* otel-collector gRPC endpoint the apps export OTLP to: internal chart-managed instance, or the
     user-supplied external one (config.otelEndpoint override), when disabled. */}}
{{- define "postyfox.otelEndpoint" -}}
{{- if .Values.otelCollector.enabled -}}
http://{{ include "postyfox.fullname" . }}-otel-collector:4317
{{- else -}}
{{ .Values.config.otelEndpoint }}
{{- end -}}
{{- end -}}
