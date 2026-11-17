#!/usr/bin/env fish
# scripts/agy_runner.fish
# Wrapper de ejecución para Gemini Flash vía agy con control automático de cuotas y reintentos.

if test (count $argv) -eq 0
    echo "Uso: scripts/agy_runner.fish <prompt-o-ruta-archivo>"
    exit 1
end

set -l input_target $argv[1]
set -l prompt_content ""

if test -f "$input_target"
    set prompt_content (cat "$input_target" | string collect)
else
    set prompt_content "$input_target"
end

if test -z "$prompt_content"
    echo "Error: El contenido del prompt está vacío."
    exit 1
end

set -l estado_file "ESTADO-ORQUESTACION.md"
set -l tmp_output ".agy_last_run.tmp"
set -l sleep_interval 900 # 15 minutos
set -l attempt 1

echo "================================================================="
echo "Iniciando agy_runner.fish (Gemini Flash Ejecutor)"
echo "Hora de inicio: "(date "+%Y-%m-%d %H:%M:%S")
echo "================================================================="

while true
    echo ""
    echo "[Intento $attempt] Ejecutando agy con gemini-3.8-flash-high..."
    
    # Ejecutar agy capturando stdout y stderr en archivo temporal
    agy -c --dangerously-skip-permissions --model gemini-3.8-flash-high -p "$prompt_content" > $tmp_output 2>&1
    set -l exit_code $status
    
    # Imprimir salida en consola
    cat $tmp_output

    set -l output_str (cat $tmp_output | string collect)

    # Evaluar si la salida contiene patrones de cuota agotada
    set -l is_quota_error 0
    if string match -qi "*429*" -- "$output_str"
        or string match -qi "*RESOURCE_EXHAUSTED*" -- "$output_str"
        or string match -qi "*quota*" -- "$output_str"
        or string match -qi "*rate limit*" -- "$output_str"
        or string match -qi "*Too Many Requests*" -- "$output_str"
        set is_quota_error 1
    end

    if test $exit_code -eq 0 -a $is_quota_error -eq 0
        set -l now (date "+%Y-%m-%d %H:%M:%S")
        echo ""
        echo "================================================================="
        echo "✅ Ejecución completada exitosamente a las $now (Intento $attempt)"
        echo "================================================================="
        
        if test -f "$estado_file"
            echo "" >> $estado_file
            echo "### ✅ Ejecución Exitosa - $now (Intento $attempt)" >> $estado_file
            echo "- **Estado:** Finalizado con código 0 sin errores de cuota." >> $estado_file
        end
        
        rm -f $tmp_output
        exit 0

    else if test $is_quota_error -eq 1
        set -l now (date "+%Y-%m-%d %H:%M:%S")
        echo ""
        echo "⚠️ [LÍMITE DE CUOTA DETECTADO] Hora: $now"
        echo "Se ha alcanzado la cuota de la ventana de 5 horas de Gemini Flash."
        echo "Pausando y esperando 15 minutos antes del próximo reintento..."
        
        if test -f "$estado_file"
            echo "" >> $estado_file
            echo "### ⏸️ Pausa por Cuota Alcanzada - $now (Intento $attempt)" >> $estado_file
            echo "- **Motivo:** Patrón de cuota detectado (429 / RESOURCE_EXHAUSTED / Rate Limit)." >> $estado_file
            echo "- **Acción:** Espera automática de 15 minutos en bucle hasta renovación." >> $estado_file
        end

        # Cuenta regresiva de 15 minutos (900s)
        set -l remaining $sleep_interval
        while test $remaining -gt 0
            set -l mins (math -s0 "$remaining / 60")
            set -l secs (math -s0 "$remaining % 60")
            printf "\r⏳ Próximo reintento en: %02dm %02ds (Intento: %d)... " $mins $secs $attempt
            sleep 10
            set remaining (math "$remaining - 10")
        end
        echo ""
        
        set attempt (math "$attempt + 1")

    else
        set -l now (date "+%Y-%m-%d %H:%M:%S")
        echo ""
        echo "❌ [ERROR CRÍTICO NO RELACIONADO A CUOTA] Hora: $now"
        echo "El comando falló con código de salida $exit_code."
        echo "Deteniendo ejecución para evitar bucle infinito."
        
        if test -f "$estado_file"
            echo "" >> $estado_file
            echo "### ❌ Error Crítico de Ejecución - $now (Intento $attempt)" >> $estado_file
            echo "- **Código de salida:** $exit_code" >> $estado_file
            echo "- **Detalle:** Error no atribuible a cuota. Detenido para revisión manual." >> $estado_file
        end
        
        exit $exit_code
    end
end
