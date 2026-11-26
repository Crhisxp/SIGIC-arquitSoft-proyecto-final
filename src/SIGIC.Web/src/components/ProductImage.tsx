"use client";

import { useState } from "react";
import Image from "next/image";

function ilustracion(nombre: string) {
  const texto = nombre.toLowerCase();
  if (/cemento/.test(texto)) return "cemento";
  if (/fierro|varilla|corrugad/.test(texto)) return "fierro";
  if (/taladro|atornillador/.test(texto)) return "herramientas";
  if (/tubo|tubería|pvc|desagüe/.test(texto)) return "tuberias";
  if (/pintura|látex|esmalte|sellador/.test(texto)) return "pinturas";
  return "ferreteria";
}

export function ProductImage({ nombre, imagenUrl, priority = false }: {
  nombre: string;
  imagenUrl?: string | null;
  priority?: boolean;
}) {
  const [failedUrl, setFailedUrl] = useState<string>();
  const fallback = `/productos/${ilustracion(nombre)}.webp`;
  const url = imagenUrl?.trim();
  const validUrl = url && /^(?:\/(?![\/\\])|https:\/\/)/i.test(url);
  const src = validUrl && url !== failedUrl ? url : fallback;

  return <Image
    src={src}
    alt={src === fallback ? `Imagen referencial de ${nombre}` : nombre}
    width={623}
    height={414}
    priority={priority}
    unoptimized
    className="aspect-[3/2] w-full rounded bg-[#ded8d1] object-contain"
    onError={() => { if (src !== fallback) setFailedUrl(src); }}
  />;
}
