using System;
using System.Collections.Generic;

namespace EditorLigasPes
{
    /// <summary>
    /// Decodificador y Codificador forense oficial de bitfields para registros de jugadores PES 2021 (Player.bin)
    /// Extraído y verificado directamente de la especificación Fox Engine y Pes-Editor-Rebuilt.
    /// </summary>
    public static class PlayerBitHelper
    {
        public static int GetBits(byte[] data, int bitPos, int numBits)
        {
            if (data == null) return 0;
            int val = 0;
            for (int i = 0; i < numBits; i++)
            {
                int p = bitPos + i;
                int byteIndex = p >> 3;
                if (byteIndex < data.Length)
                {
                    int bitIndex = p & 7;
                    if (((data[byteIndex] >> bitIndex) & 1) != 0)
                    {
                        val |= (1 << i);
                    }
                }
            }
            return val;
        }

        public static void SetBits(byte[] data, int bitPos, int numBits, int value)
        {
            if (data == null) return;
            for (int i = 0; i < numBits; i++)
            {
                int p = bitPos + i;
                int byteIndex = p >> 3;
                if (byteIndex < data.Length)
                {
                    int bitIndex = p & 7;
                    int mask = 1 << bitIndex;
                    if (((value >> i) & 1) != 0)
                        data[byteIndex] |= (byte)mask;
                    else
                        data[byteIndex] &= (byte)~mask;
                }
            }
        }

        // Habilidades en Player.bin (bit, 6 bits, sumar 40)
        public static readonly Dictionary<string, int> DbAbilBits = new Dictionary<string, int>
        {
            { "atk", 370 },
            { "drib", 352 },
            { "loftpass", 402 },
            { "finish", 396 },
            { "place_kick", 250 },
            { "swerve", 332 },
            { "kick_pwr", 358 },
            { "speed", 306 },
            { "exp_pwr", 344 },
            { "stamina", 338 },
            { "def", 275 },
            { "ball_win", 312 },
            { "aggres", 384 },
            { "jump", 294 },
            { "header", 288 },
            { "lowpass", 263 },
            { "body_ctrl", 376 },
            { "phys_cont", 390 },
            { "ball_ctrl", 281 },
            { "tight_pos", 416 },
            { "gk", 326 },
            { "catching", 364 },
            { "clearing", 269 },
            { "reflex", 320 },
            { "cover", 300 }
        };

        public static int GetAbility(byte[] data, string key)
        {
            int bit;
            if (data != null && DbAbilBits.TryGetValue(key, out bit))
            {
                return GetBits(data, bit, 6) + 40;
            }
            if (data == null) return 70;
            // Clave desconocida: antes devolvía 70 plausible y enmascaraba typos (M120/B65).
            throw new ArgumentException("Clave de habilidad desconocida: " + key, "key");
        }

        public static void SetAbility(byte[] data, string key, int value)
        {
            int bit;
            if (data != null && DbAbilBits.TryGetValue(key, out bit))
            {
                int clamped = Math.Max(40, Math.Min(103, value)) - 40; // 6 bits: 40-103, igual que Get (M119)
                SetBits(data, bit, 6, clamped);
            }
        }

        // Físico y Datos Básicos en Player.bin
        public static int GetHeight(byte[] data)
        {
            if (data == null || data.Length < 0x20) return 175;
            return data[0x1B] + 100;
        }

        public static void SetHeight(byte[] data, int height)
        {
            if (data != null && data.Length > 0x1B)
            {
                data[0x1B] = (byte)Math.Max(0, Math.Min(255, height - 100));
            }
        }

        public static int GetWeight(byte[] data)
        {
            if (data == null) return 70;
            return GetBits(data, 256, 7) + 30;
        }

        public static void SetWeight(byte[] data, int weight)
        {
            if (data != null)
            {
                SetBits(data, 256, 7, Math.Max(0, Math.Min(127, weight - 30)));
            }
        }

        public static ushort GetNationalityId(byte[] data)
        {
            if (data == null) return 0;
            return (ushort)GetBits(data, 233, 9);
        }

        public static void SetNationalityId(byte[] data, ushort natId)
        {
            if (data != null)
            {
                // 9 bits: > 511 daba vueltas en silencio (M119).
                if (natId > 511)
                    throw new ArgumentOutOfRangeException("natId", "La nacionalidad no cabe en 9 bits (máximo 511).");
                SetBits(data, 233, 9, (int)natId);
            }
        }

        public static int GetAge(byte[] data)
        {
            if (data == null) return 25;
            return GetBits(data, 408, 6) + 15;
        }

        public static void SetAge(byte[] data, int age)
        {
            if (data != null)
            {
                SetBits(data, 408, 6, Math.Max(0, Math.Min(63, age - 15)));
            }
        }

        public static bool IsLeftFooted(byte[] data)
        {
            if (data == null) return false;
            return GetBits(data, 514, 1) != 0;
        }

        public static void SetFoot(byte[] data, bool isLeft)
        {
            if (data != null)
            {
                SetBits(data, 514, 1, isLeft ? 1 : 0);
            }
        }

        public static int GetWeakFootUsage(byte[] data)
        {
            if (data == null) return 2;
            return GetBits(data, 454, 2) + 1;
        }

        public static void SetWeakFootUsage(byte[] data, int val)
        {
            if (data != null)
            {
                SetBits(data, 454, 2, Math.Max(0, Math.Min(3, val - 1)));
            }
        }

        public static int GetWeakFootAccuracy(byte[] data)
        {
            if (data == null) return 2;
            return GetBits(data, 462, 2) + 1;
        }

        public static void SetWeakFootAccuracy(byte[] data, int val)
        {
            if (data != null)
            {
                SetBits(data, 462, 2, Math.Max(0, Math.Min(3, val - 1)));
            }
        }

        public static int GetForm(byte[] data)
        {
            if (data == null) return 5;
            return GetBits(data, 438, 3) + 1;
        }

        public static void SetForm(byte[] data, int val)
        {
            if (data != null)
            {
                SetBits(data, 438, 3, Math.Max(0, Math.Min(7, val - 1)));
            }
        }

        public static int GetInjuryResistance(byte[] data)
        {
            if (data == null) return 1;
            return GetBits(data, 458, 2); // 0=A, 1=B, 2=C
        }

        public static void SetInjuryResistance(byte[] data, int val)
        {
            if (data != null)
            {
                SetBits(data, 458, 2, Math.Max(0, Math.Min(3, val))); // 2 bits: 0-3, igual que Get (M119)
            }
        }

        // Habilidades Especiales en Player.bin (DB_SKILL)
        public static readonly Dictionary<int, int> DbSkillBits = new Dictionary<int, int>
        {
            { 0, 519 },  // Bicicleta
            { 28, 523 }, // Doble toque
            { 1, 485 },  // Elástica
            { 2, 497 },  // Ruleta
            { 3, 482 },  // Sombrero
            { 29, 481 }, // Giro cruzado
            { 4, 528 },  // Recorte y giro
            { 5, 525 },  // Amago de tiro
            { 30, 500 }, // Pisar el balón
            { 10, 505 }, // Taconazo
            { 6, 495 },  // Cabezazo
            { 7, 527 },  // Disparo potente lejano
            { 31, 506 }, // Vaselina
            { 39, 518 }, // Tiro lejano
            { 8, 513 },  // Disparo sin rosca
            { 32, 494 }, // Disparo con caída
            { 33, 499 }, // Disparo ascendente
            { 9, 524 },  // Remate acrobático
            { 11, 511 }, // Disparo al primer toque
            { 12, 507 }, // Pase al primer toque
            { 40, 487 }, // Pase en profundidad
            { 13, 484 }, // Pase medido
            { 14, 483 }, // Centro preciso
            { 15, 493 }, // Rosca exterior
            { 16, 515 }, // Rabona
            { 34, 512 }, // Pase sin mirar
            { 17, 488 }, // Pase bombeado bajo
            { 19, 521 }, // Saque de banda largo
            { 18, 490 }, // Saque bajo de portero
            { 35, 496 }, // Saque alto de portero
            { 20, 522 }, // Saque largo con la mano (GK)
            { 37, 502 }, // Atajapenales (GK)
            { 36, 501 }, // Especialista en penales
            { 21, 491 }, // Picardía
            { 22, 504 }, // Marcaje al hombre
            { 23, 517 }, // Repliegue
            { 38, 503 }, // Intercepción
            { 24, 530 }, // Despeje acrobático
            { 25, 492 }, // Capitanía
            { 26, 516 }, // Revulsivo
            { 27, 486 }  // Espíritu de lucha
        };

        public static readonly Dictionary<int, string> SkillLabels = new Dictionary<int, string>
        {
            { 0, "Bicicleta" }, { 28, "Doble toque" }, { 1, "Elástica" }, { 2, "Ruleta" }, { 3, "Sombrero" },
            { 29, "Giro cruzado" }, { 4, "Recorte y giro" }, { 5, "Amago de tiro" }, { 30, "Pisar el balón" }, { 10, "Taconazo" },
            { 6, "Cabezazo" }, { 7, "Disparo potente lejano" }, { 31, "Vaselina" }, { 39, "Tiro lejano" }, { 8, "Disparo sin rosca" },
            { 32, "Disparo con caída" }, { 33, "Disparo ascendente" }, { 9, "Remate acrobático" }, { 11, "Disparo al primer toque" },
            { 12, "Pase al primer toque" }, { 40, "Pase en profundidad" }, { 13, "Pase medido" }, { 14, "Centro preciso" },
            { 15, "Rosca exterior" }, { 16, "Rabona" }, { 34, "Pase sin mirar" }, { 17, "Pase bombeado bajo" }, { 19, "Saque de banda largo" },
            { 18, "Saque bajo (GK)" }, { 35, "Saque alto (GK)" }, { 20, "Saque largo con la mano (GK)" }, { 37, "Atajapenales (GK)" },
            { 36, "Especialista en penales" }, { 21, "Picardía" }, { 22, "Marcaje al hombre" }, { 23, "Repliegue" },
            { 38, "Intercepción" }, { 24, "Despeje acrobático" }, { 25, "Capitanía" }, { 26, "Revulsivo" }, { 27, "Espíritu de lucha" }
        };

        public static bool HasSkill(byte[] data, int skillId)
        {
            int bit;
            if (data != null && DbSkillBits.TryGetValue(skillId, out bit))
            {
                return GetBits(data, bit, 1) != 0;
            }
            return false;
        }

        public static void SetSkill(byte[] data, int skillId, bool enable)
        {
            int bit;
            if (data != null && DbSkillBits.TryGetValue(skillId, out bit))
            {
                SetBits(data, bit, 1, enable ? 1 : 0);
            }
        }

        // Posiciones compatibilidad en Player.bin (DB_POSFLAG: 2 bits cada una)
        public static readonly Dictionary<int, int> DbPosBits = new Dictionary<int, int>
        {
            { 0, 350 },  // GK
            { 1, 468 },  // CB
            { 2, 318 },  // LB
            { 3, 474 },  // RB
            { 4, 414 },  // DMF
            { 5, 456 },  // CMF
            { 6, 466 },  // LMF
            { 7, 460 },  // RMF
            { 8, 464 },  // AMF
            { 9, 472 },  // LWF
            { 10, 476 }, // RWF
            { 11, 478 }, // SS
            { 12, 470 }  // CF
        };

        public static int GetPositionCompatibility(byte[] data, int posIndex)
        {
            int bit;
            if (data != null && DbPosBits.TryGetValue(posIndex, out bit))
            {
                return GetBits(data, bit, 2);
            }
            return 0;
        }

        public static void SetPositionCompatibility(byte[] data, int posIndex, int level)
        {
            int bit;
            if (data != null && DbPosBits.TryGetValue(posIndex, out bit))
            {
                SetBits(data, bit, 2, Math.Max(0, Math.Min(3, level))); // 2 bits: 0-3, igual que Get (M120/B65)
            }
        }

        public static int GetPrimaryPosition(byte[] data)
        {
            if (data == null || data.Length <= 0x36) return 12; // CF
            return (data[0x36] >> 2) & 0x0F;
        }

        public static void SetPrimaryPosition(byte[] data, int posIndex)
        {
            if (data != null && data.Length > 0x36)
            {
                data[0x36] = (byte)((data[0x36] & 0xC3) | ((posIndex & 0x0F) << 2));
            }
        }

        // Estilos de Juego (bit 155, 5 bits) y Estilos COM/IA en Player.bin
        public static readonly string[] PlayingStyleNames = new string[]
        {
            "Ninguno", "Cazagoles", "Señuelo", "Hombre de área", "Hombre objetivo",
            "Creador de juego", "Extremo prolífico", "Extremo móvil", "Especialista en centros",
            "Enganche clásico", "Jugador de huecos", "Todocampista", "El destructor",
            "Organizador", "Medio defensivo", "Lateral ofensivo", "Lateral finalizador",
            "Lateral defensivo", "Salida de balón", "Atacante extra", "Portero ofensivo", "Portero defensivo"
        };

        public static readonly int[] DbStyle = new int[]
        {
            0, 1, 2, 3, 6, 9, 10, 11, 14, 12, 19, 15, 17, 4, 5, 18, 20, 21, 7, 8, 13, 16
        };

        public static int GetPlayingStyle(byte[] data)
        {
            if (data == null) return 0;
            int raw = GetBits(data, 155, 5);
            if (raw >= 0 && raw < DbStyle.Length)
                return DbStyle[raw];
            return 0;
        }

        public static void SetPlayingStyle(byte[] data, int styleId)
        {
            if (data == null) return;
            for (int i = 0; i < DbStyle.Length; i++)
            {
                if (DbStyle[i] == styleId)
                {
                    SetBits(data, 155, 5, i);
                    return;
                }
            }
            SetBits(data, 155, 5, 0);
        }

        public static readonly string[] ComStyleNames = new string[]
        {
            "Malabarista", "Regate en carrera", "Bala", "Desmarque incisivo",
            "Experto en pase largo", "Centro temprano", "Tirador lejano"
        };

        public static readonly int[] DbComBits = new int[] { 489, 531, 526, 510, 529, 447, 520 };

        public static bool HasComStyle(byte[] data, int comIndex)
        {
            if (data != null && comIndex >= 0 && comIndex < DbComBits.Length)
            {
                return GetBits(data, DbComBits[comIndex], 1) != 0;
            }
            return false;
        }

        public static void SetComStyle(byte[] data, int comIndex, bool enable)
        {
            if (data != null && comIndex >= 0 && comIndex < DbComBits.Length)
            {
                SetBits(data, DbComBits[comIndex], 1, enable ? 1 : 0);
            }
        }
    }
}
