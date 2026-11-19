export interface ConsultaAnaliticaRequest {
  pregunta: string;
  desde: string;
  hastaExclusivo: string;
}

export interface ConsultaAnaliticaResponse {
  estado: "con_datos" | "sin_datos" | "datos_incompletos" | "requiere_aclaracion";
  metrica: string;
  hechos: { etiqueta: string; valor: string; unidad: string }[];
  resumen: string;
  corteUtc: string;
}

export interface StaffUser {
  username: string;
  roles: string[];
}
