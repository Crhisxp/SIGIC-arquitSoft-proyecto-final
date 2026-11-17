#!/usr/bin/env fish
# Flujo híbrido Orquestador-Codificador-Auditor de SIGIC Ferretería v2.2.

function run_codex_with_retry
    set -l LOG_FILE /tmp/codex_exec.log
    set -l WAIT_COUNT 0
    set -l MAX_WAITS 2

    while true
        codex exec $argv > "$LOG_FILE" 2>&1
        set -l CODEX_STATUS $status
        cat "$LOG_FILE"

        if test $CODEX_STATUS -eq 0
            return 0
        end
        if test $WAIT_COUNT -ge $MAX_WAITS
            return $CODEX_STATUS
        end
        if not string match -qir -- \
                'rate limit|quota|usage limit|429|try again in|too many requests' < "$LOG_FILE"
            return $CODEX_STATUS
        end

        set WAIT_COUNT (math "$WAIT_COUNT + 1")
        echo "Pausa de Codex: "(date '+%Y-%m-%d %H:%M:%S')
        echo "Límite de cuota/tokens: esperando 5 horas y 2 minutos (18120 segundos) para la renovación de ventana de Codex. Espera $WAIT_COUNT/$MAX_WAITS."
        sleep 18120; or return $status
    end
end

if test (count $argv) -lt 1
    echo 'Uso: scripts/orquestador_sdd.fish "Objetivo de la fase"' >&2
    exit 2
end
if test -z (string trim -- "$argv[1]")
    echo 'Error: el objetivo de la fase no puede estar vacío.' >&2
    exit 2
end

set -l OBJETIVO "$argv[1]"
set -l PROMPT_FILE /tmp/sigic_handoff_prompt.txt
set -l REPORT_FILE /tmp/sigic_handoff_report.md
set -l AUDIT_FILE /tmp/sigic_audit_verdict.txt
set -l MAX_LOOPS 2

# Resolver las rutas del repositorio incluso al invocar desde otro directorio.
set -l SCRIPT_DIR (path dirname (status filename))
cd "$SCRIPT_DIR/.."; or exit 1
if not test -x ./scripts/agy_runner.fish
    echo 'Error: scripts/agy_runner.fish no existe o no es ejecutable.' >&2
    exit 1
end

printf '' > "$PROMPT_FILE"; or exit 1
echo '[Paso 1] Planificación con GPT-6-Astra...'
run_codex_with_retry -m gpt-6-astra --sandbox read-only -o "$PROMPT_FILE" \
    "Eres el Orquestador de SIGIC Ferretería v2.2. Objetivo de la fase: $OBJETIVO
Lee AGENTS.md, DESIGN.md y las especificaciones relevantes para este objetivo.
Redacta únicamente el prompt maestro ejecutable para Gemini Flash mediante
scripts/agy_runner.fish. Tu salida final será guardada en $PROMPT_FILE por -o;
no modifiques archivos ni intentes escribir el prompt mediante herramientas.
El prompt debe incluir el objetivo, alcance, pasos atómicos y criterios verificables.
Exige TDD/SDD estricto: leer especificaciones, demostrar RED, implementar GREEN y
refactorizar con pruebas reales. Respetar Clean Architecture e invariantes de AGENTS.md.
Preservar el DAG canónico: 79 nodos, 96 aristas, 0 ciclos y máximo 4 enlaces salientes
por nota. Validarlo con evidencia real y usar enlaces Markdown relativos sin wikilinks.
Leer y aplicar las skills y herramientas MCP pertinentes al alcance; registrar sus
resultados reales, sin inventar ejecuciones ni declarar éxito cuando falte evidencia.
Preservar cambios preexistentes ajenos al objetivo y actualizar las bitácoras exigidas
por AGENTS.md. Al finalizar, escribir un reporte técnico de máximo 80 líneas en
$REPORT_FILE, con archivos modificados, evidencia real de pruebas/MCP (comandos,
códigos de salida, resultados y métricas del DAG) y pendientes o bloqueos explícitos."
set -l PLAN_STATUS $status
if test $PLAN_STATUS -ne 0
    echo "Error: la planificación falló (código $PLAN_STATUS)." >&2
    exit $PLAN_STATUS
end
if not test -s "$PROMPT_FILE"
    echo 'Error: el planificador no generó un prompt.' >&2
    exit 1
end

for ATTEMPT in (seq 1 $MAX_LOOPS)
    # Invalidar resultados anteriores para exigir evidencia de este intento.
    printf '' > "$REPORT_FILE"; or exit 1
    printf '' > "$AUDIT_FILE"; or exit 1
    echo "[Paso 2, intento $ATTEMPT/$MAX_LOOPS] Ejecución con Gemini Flash..."
    ./scripts/agy_runner.fish "$PROMPT_FILE"
    set -l RUN_STATUS $status
    if test $RUN_STATUS -ne 0
        echo "Error: el ejecutor falló (código $RUN_STATUS)." >&2
        exit $RUN_STATUS
    end

    echo "[Paso 3, intento $ATTEMPT/$MAX_LOOPS] Auditoría con GPT-6.1-Sol..."
    run_codex_with_retry -m gpt-6.1-sol --sandbox read-only -o "$AUDIT_FILE" \
        "Eres el Auditor de SIGIC Ferretería v2.2. Objetivo de la fase: $OBJETIVO
Lee AGENTS.md, DESIGN.md y las especificaciones aplicables. Audita $REPORT_FILE
(debe existir, tener contenido y como máximo 80 líneas). Ejecuta git status --short
y git diff; inspecciona también los archivos nuevos no rastreados y los cambios
preparados con git diff --cached. Contrasta los cambios y la evidencia real con el
objetivo, AGENTS.md, Clean Architecture, TDD/SDD y las skills/MCP aplicables.
Verifica el DAG canónico: 79 nodos, 96 aristas, 0 ciclos, máximo 4 enlaces salientes
por nota. Rechaza cualquier error, prueba faltante o evidencia insuficiente; no
aceptes afirmaciones de éxito sin evidencia. No modifiques archivos del repositorio.
Emite únicamente el veredicto, sin encabezados ni bloques de código. Tu salida final
será guardada por -o en $AUDIT_FILE; no intentes escribirla mediante herramientas.
Si todo cumple, la primera línea debe comenzar exactamente con APROBADO: seguida
de un resumen en 5 líneas.
Si detectas fallos, la primera línea debe comenzar exactamente con RECHAZADO:
seguida del prompt quirúrgico exacto y autosuficiente para que Gemini Flash corrija
los fallos, conserve el objetivo y las restricciones originales, repita las pruebas
y MCP necesarios y reescriba $REPORT_FILE con evidencia real en máximo 80 líneas."
    set -l AUDIT_STATUS $status
    if test $AUDIT_STATUS -ne 0
        echo "Error: la auditoría falló (código $AUDIT_STATUS)." >&2
        exit $AUDIT_STATUS
    end

    set -l FIRST_LINE (head -n 1 "$AUDIT_FILE")
    if string match -q -- 'APROBADO:*' "$FIRST_LINE"
        cat "$AUDIT_FILE"
        exit 0
    else if string match -q -- 'RECHAZADO:*' "$FIRST_LINE"
        cp -- "$AUDIT_FILE" "$PROMPT_FILE"; or exit 1
        echo "Auditoría rechazada en el intento $ATTEMPT/$MAX_LOOPS." >&2
    else
        echo "Error: veredicto vacío o inválido en $AUDIT_FILE." >&2
        exit 1
    end
end

cat "$AUDIT_FILE"
echo "Error: no se obtuvo aprobación tras $MAX_LOOPS intentos." >&2
exit 1
