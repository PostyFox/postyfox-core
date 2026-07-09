{{- define "postyfox.fullname" -}}
{{- printf "%s-postyfox" .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "postyfox.labels" -}}
app.kubernetes.io/part-of: postyfox
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end -}}

{{- define "postyfox.image" -}}
{{- $svc := index . 1 -}}
{{- with (index . 0) -}}
{{ .Values.image.registry }}/{{ .Values.image.repository }}-{{ $svc }}:{{ .Values.image.tag }}
{{- end -}}
{{- end -}}

{{- define "postyfox.secretName" -}}
{{ include "postyfox.fullname" . }}-secrets
{{- end -}}
