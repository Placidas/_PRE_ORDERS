using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Threading;
using System.IO;
using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Payments;
using Resto.Front.Api;

/*                                      IŠANKSTINIŲ SĄSKAITŲ INFORMACIJOS, GAUNAMOS IŠ 'SPSFPLib.dll', APDOROJIMAS
 Terminai: Užsakymo Nr - Syrve Order Number, Išankstinės sąskaitos Nr - išspausdinti kvito dokumento Nr
           -----------                       ------------------------
 Programa veikia cikle tikrindama, ar atsirado kataloge 'C:\SPS\IKS-T81F\Pre_Orders' reikiamas apdoroti (įeinantis) failas ir grąžina suformuotą rezultato failą
 Siekiant informacijos apdorojimo stabilumo:
   -radus įeinantį failą prieš jį atidarant palaukiama 0,5 sek., kad programoje 'SPSFPLib.dll' būtų pilnai suformuotas ir uždarytas failas
   -rezultato failas pradžioje pilnai suformuojamas ir uždaromas ir tik tada panaikinamas įeinantis failas (tai požymis, kad apdorojimas užbaigtas)
 Išankstinių sąskaitų bazė yra tekstinis failas (atskiras kiekvienai Z ataskaitai / pamainai) 'C:\SPS\IKS-T81F\Pre_Orders\Pre_orders_xxx.csv' (xxx Z ataskaitos Nr)
 Spausdinant pirmą išankstinę pamainos pradžioje sukuriamas naujas failas, kuris toliau papildomas (viena sąskaita - vienas įrašas)
 Bazės laukai atskirti <TAB> ženklais, įrašai išdėstyti įrašymo (išankstinės sąskaitos spausdinimo) laiko didėjimo tvarka:
    +---------------------------------------+-------+---------------------+------------------------------------------------------+
    |       Laukas                          | Ilgis |       Pavyzdys      |                         Pastabos                     |
    +---------------------------------------+-------+---------------------+------------------------------------------------------+
    |Įrašymo laikas                         |    19 | 2024-02-07 15:40:41 |                                                      |
    |Užsakymo nr                            |     8 |      378            | Priekyje tarpai                                      |
    |Sąskaitos (dokumento) nr               |     8 |     1048            | Priekyje tarpai                                      |
    |Pirminės sąskaitos dokumento nr        |     8 |      377            | Priekyje tarpai, jei nėra pirminės sąskaitos - 0     |
    |Suma išankstinėje sąskaitoje (centais) |     8 |     1220            | Priekyje tarpai                                      |
    |Apmokėta suma (centais)                |     8 |     1220            | Priekyje tarpai, jei neapmokėta - 0                  |
    |Amokėjimo data                         |    19 | 2024-02-07 16:12:41 | Jei neapmokėta 0000-00-00 00:00:00                   |
    +---------------------------------------+-------+---------------------+------------------------------------------------------+

Įeinančių ir rezultato failų yra kelių tipų priklausomai atliekamos funkcijos. Failai yra kataloge 'C:\SPS\IKS-T81F\Pre_Orders\'
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+
    |  Failų vardai    |         Sąlyga       |                                 SPSFPLib ir Plugin veiksmai                                               |
    +-----------------------------------------+-----------------------------------------------------------------------------------------------------------+
    |                  |Pradedamas spausdinti |Jeigu nefiskalinio kvito pirma eilutė "IŠANKSTINĖ SĄSKAITA", o antra "I­šankstinės sąskaitos Nr xxx":       |
    |                  |nefiskalinis kvitas   | -pasižymima, kad tai išankstinės sąskaitos tipo kvitas, atsimenamas išankstinės sąskaitos Nr.             |
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+
    |Pre_order_add.inp |Baigiamas spausdinti  |Jeigu randama, kad šis nefiskalinis kvitas yra išankstinė sąskaita, po pasirašymo:                         |
    |Pre_order_add.out |nefiskalinis išanksti-| -Syrve surandama išankstinės sąskaitos suma suma ir nustatoma, ar yra pirminis užsakymas,                 |
    |                  |nės sąskaitos kvitas  | -papildoma išankstinių sąskaitų bazė (atsiskaitymo suma ir data yra nuliai)                               |
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+
    |Pre_order_beg.inp |Pradedamas spausdinti |Jeigu fiskalinio kvito pirma eilutė yra "#Stalas: nn Užsakymas: nnnn":                                     |
    |Pre_order_beg.out |fiskalinis kvitas su  | -bazėje ieškomos sąskaitos (neapmokėtos) su šiuo ir pirminiu numeriu                                      |
    |                  |išankstine sąskaita   | -fiskaliniame kvite pridedamos eilutės "I­šankstinės sąskaitos Nr"                                         |
    |                  |                      | -išsaugomas išankstinių sąskaitų dokumento numerių sąrašas (skyriklis - kabliataškis)                     |
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+
    |Pre_order_end.inp |Baigiamas spausdinti  |Jeigu randama, kad šis fiskalinis kvitas yra su išankstine sąskaita, po pasirašymo:                        |
    |Pre_order_end.out |fiskalinis kvitas su  | -bazėje išankstinių sąskaitų įrašuose (pagal dokumento numerių sąrašą) įvedama atsiskaitymo data ir suma. |
    |                  |išankstine sąskaita   |                                                                                                           |
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+
    |Pre_order_x_z.inp |Skaičiuoti neapmokėtas|Prieš spausdinant X arba Z ataskaitas peržiūrimi visi bazės neapmokėti įrašai:                             |
    |Pre_order_x_z.out |sąskaitas spausdinant | -grąžina kiekį ir sumą (dvi poros): perkelta į viešbutį (nuliai) ir anuliuota (kiekis ir suma)            |
    |                  |X arba Z ataskaitą    |                                                                                                           |
    +------------------+----------------------+-----------------------------------------------------------------------------------------------------------+    */

namespace PRE_ORDERS
{
    internal sealed class PRE_ORDERS_Dialog : IDisposable
    {
        private const string PluginName = "PRE_ORDERS";

        public PRE_ORDERS_Dialog()
        {
            var windowThread = new Thread(EntryPoint);
            windowThread.SetApartmentState(ApartmentState.STA);
            windowThread.Start();
        }
        public static int GLOBAL_LEVEL = 3;  // 0-neprotokoluoti, 1-tik sumavimas, 2 - pirminio paieška ir sumavimas, 3 - detaliai
        public static string PRE_ORDERS_DIR = "\\SPS\\IKS-T81F\\Pre_Orders";
        public static string PreCheck_Nr = "Išankstinės sąskaitos N: ";
        public static string PreCheck_Sum = "VISO MOKĖTI:             ";
        public static string PayCheck_Nr = "#Stalas: ? Užsakymas: ";
        public static string PayCheck_Nr2 = "#Pirminis užsakymo Nr: ";
        public static string PayCheck_Sum = "Mokėti                                         ";
        public static string Hotel_Payment_Name = "Bankinės kortelės";
        public static string FILE_TIP;
        public static string
                sINP,       // Užklausos įeinanti eilutė
                sOUT,       // Užklausos grąžinama eilutė
                ord_nr,     // Užsakymo Nr
                ord_dok,    // Užsakymo dokumento Nr
                ord_Z,      // Z ataskaitos Nr
                ord_sum,    // Sąskaitos suma
                pirm_ord_nr,// Pirminio užsakymo Nr
                pirm_ord_dok,//Pirminio užsakymo dokumento Nr
                ord_pay_sum,// Suma apmokant užsakymą ("padengiant" išankstinę sąskaitą)
                line, line_pay, all_lines,
                spaces = "        ";
        public static StreamReader SR;
        public static StreamWriter SW;

        public static void LOG(string txt, int level)
        {
            if (level > GLOBAL_LEVEL) return;
            if (!Directory.Exists(PRE_ORDERS_DIR + "\\LOG")) System.IO.Directory.CreateDirectory(PRE_ORDERS_DIR + "\\LOG");
            string LOG_FILE = PRE_ORDERS_DIR + "\\LOG\\" + DateTime.Now.ToString("yyyy-MM-dd") + ".LOG";
            StreamWriter sw_log;
            if (!File.Exists(LOG_FILE))
            {
                sw_log = new StreamWriter(LOG_FILE, false, System.Text.Encoding.GetEncoding("iso-8859-1"));
                sw_log.Write("\xEF\xBB\xBF");
                sw_log.Close();
            }
            sw_log = new StreamWriter(LOG_FILE, true);
            sw_log.Write(DateTime.Now.ToString("HH:mm:ss") + " " + txt + "\r\n");
            sw_log.Close();
        }
        public static bool LOG_BEG(string tip)
        {
            if (!File.Exists(PRE_ORDERS_DIR + "\\Pre_order_" + (FILE_TIP = tip) + ".inp")) return false;
            Thread.Sleep(500);
            SR = new StreamReader(PRE_ORDERS_DIR + "\\Pre_order_" + FILE_TIP + ".inp");
            sINP = SR.ReadLine();
            SR.Close();
            LOG("->" + FILE_TIP + ":" + sINP, 3);
            return true;
        }
        public static void LOG_END()
        {
            LOG("<-" + FILE_TIP + ":" + sOUT, 3);
            SW = new StreamWriter(PRE_ORDERS_DIR + "\\Pre_order_" + FILE_TIP + ".out");
            SW.WriteLine(sOUT);
            SW.Close();
            File.Delete(PRE_ORDERS_DIR + "\\Pre_order_" + FILE_TIP + ".inp");
        }
        public static string DB_FILE_TIP()
        {
            return (PRE_ORDERS_DIR + "\\Pre_orders_" + ord_Z + ".csv");
        }
        private void EntryPoint()
        {
            LOG("Start", 1);
            IOrder order, parent_order;
            Guid? par_id;
            int i, iNUMBER, iPARENT_NUMBER = 0;
            for (; ; Thread.Sleep(100))
            {
                /*********************************************************************************************************************************************/
                /*                            Pirminio prečekio suradimas ir prečekio įrašymas į bazę                                                        */
                /*   Pre_order_add.inp - ord_Z;ord_dok,ord_nr                                                                                                */
                /*   Pre_order_add.out - SUMA (0.00)                                                                                                         */
                /*********************************************************************************************************************************************/
                if (LOG_BEG("add"))
                {
                    if ((i = sINP.IndexOf(";")) <= 0) 
                    {
                        sOUT = "  ERROR - Nenurodytas sąskaitos dokumento nr";
                        goto Pre_order_add_END;
                    }
                    ord_Z = sINP.Remove(i);
                    sINP = sINP.Substring(i + 1);
                    if ((i = sINP.IndexOf(";")) <= 0)
                    {
                        sOUT = "  ERROR - Nenurodytas sąskaitos Z-nr";
                        goto Pre_order_add_END;
                    }
                    ord_dok = sINP.Remove(i);
                    ord_nr = sINP.Substring(i + 1);
                    for (i = iNUMBER = 0; i < ord_nr.Length; i++) if (ord_nr[i] < '0' || ord_nr[i] > '9') break; else iNUMBER = iNUMBER * 10 + ord_nr[i] - '0';
                    if (iNUMBER == 0)
                    {
                        sOUT = "  ERROR - Nurodytas sąskaitos nr = 0";
                        goto Pre_order_add_END;
                    }
                    pirm_ord_nr = pirm_ord_dok = "       0";
                    iPARENT_NUMBER = 0;
                    sOUT = "0";
                    order = PluginContext.Operations.GetOrders().Last(o => o.Number == iNUMBER);
                    if (order == null)
                    {
                        sOUT = "  ERROR - Syrve nerastas užsakymas Nr " + iNUMBER.ToString();
                        goto Pre_order_add_END;
                    }
                    ord_sum = order.ResultSum.ToString("0.00");
                    LOG("  Pre-OrderId = " + (order != null ? order.Id.ToString() : "null"), 3);
                    par_id = null;
                    par_id = order.ParentOrderId;
                    LOG("  Pre-ParentOrderId = " + (par_id != null ? par_id.ToString() : "null"), 3);
                    if (par_id == null) goto Pre_order_inp_END;
                    parent_order = PluginContext.Operations.TryGetOrderById((Guid)par_id);
                    if (parent_order == null)
                    {
                        LOG("  Syrve nesurastas ParentOrder", 3);
                        goto Pre_order_inp_END;
                    }
                    iPARENT_NUMBER = parent_order.Number;
                    LOG("  Pre-ParentOrder Nr:" + iPARENT_NUMBER.ToString(), 3);
                    // PAIEŠKA, ar nėra tolesnio (gilesnio) pirminio užsakymo  ...

                    // Ieškome pirminio užsakymo dokumento Nr
                    if (!File.Exists(DB_FILE_TIP()))
                    {
                        LOG("  ERROR - Surasta pirminė sąskaita, bet nėra bazės", 3);
                        goto Pre_order_inp_END;           // Išankstinių sąskaitų bazės nėra
                    }
                    SR = new StreamReader(DB_FILE_TIP());
                    line = null;
                    pirm_ord_nr = iPARENT_NUMBER.ToString();
                    pirm_ord_nr = spaces.Remove(8 - pirm_ord_nr.Length) + pirm_ord_nr;
                    while ((line = SR.ReadLine()) != null)
                        if (line.Length > 80) if (line.Substring(20, 8) == pirm_ord_nr && line.Substring(65, 1) == "0") break;
                    SR.Close();
                    if (line == null)
                    {
                        LOG("  ERROR - Surasta pirminė sąskaita, bet bazėje nėra tokio užsakymo", 3);
                        goto Pre_order_inp_END;                               // Išankstinė sąskaita nesurasta
                    }
                    pirm_ord_dok = line.Substring(29, 8);                     // Išankstinės sąskaitos dokumento nr
                Pre_order_inp_END:
                    sOUT = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + 
                           spaces.Remove(8 - ord_nr.Length) + ord_nr + "\t" +
                           spaces.Remove(8 - ord_dok.Length) + ord_dok + "\t" +
                           pirm_ord_dok + "\t" +
                           spaces.Remove(8 - ord_sum.Length) + ord_sum + "\t" +
                           "       0\t" +
                           "0000-00-00 00:00:00";
                    SW = new StreamWriter(DB_FILE_TIP(), true);
                    SW.WriteLine(sOUT);
                    SW.Close();
                    sOUT = order.ResultSum.ToString("0.00").Replace(",", ".");
                Pre_order_add_END:
                    LOG_END();
                    continue;
                }

                /*********************************************************************************************************************************************/
                /*                 Fiskalinio kvito spausdinimo pradžia - formuojamas neapmokėtų sąskaitų sąrašas                                            */
                /*   Pre_order_beg.inp - Z-nr;sąsk-nr                                                                                                        */
                /*   Pre_order_beg.out - 'dok' (pirminės nėra) arba 'dok;sąsk-1;sąsk_2;...  (dok= '0') - sąskaitų nėra                                       */
                /*********************************************************************************************************************************************/
                if (LOG_BEG("beg"))
                {
                    if ((i = sINP.IndexOf(";")) <= 0)
                    {
                        sOUT = "Užklausoje nenurodytas Z-nr";
                        goto Pre_order_beg_END;
                    }
                    ord_Z = sINP.Remove(i);
                    ord_nr = sINP.Substring(i + 1);
                    PRE_ORDER_NOPAID_LIST();
                Pre_order_beg_END:
                    LOG_END();
                    continue;
                }

                /*********************************************************************************************************************************************/
                /*        Fiskalinio kvito spausdinimo užbaigimas (po pasirašymo) - atžymėjimas fiskalinio kvito sumos ir apmokėjimo datos                   */
                /*   Pre_order_beg.end - Z-nr;sąsk-nr                                                                                                        */
                /*********************************************************************************************************************************************/
                if (LOG_BEG("end"))
                {
                    if ((i = sINP.IndexOf(";")) <= 0)
                    {
                        sOUT = "Užklausoje nenurodytas Z-nr";
                        goto Pre_order_end_END;
                    }
                    ord_Z = sINP.Remove(i);
                    if (!File.Exists(DB_FILE_TIP()))
                    {
                        sOUT = "   Nerasta išankstinių sąskaitų bazė '" + DB_FILE_TIP() + "'";
                        goto Pre_order_end_END;
                    }
                    sINP = sINP.Substring(i + 1);
                    if ((i = sINP.IndexOf(";")) <= 0)
                    {
                        sOUT = "Užklausoje nenurodytas sąsk-nr";
                        goto Pre_order_end_END;
                    }
                    ord_nr = sINP.Remove(i);
                    ord_pay_sum = sINP.Substring(i + 1);
                    ord_pay_sum = ord_pay_sum.Remove(ord_pay_sum.Length - 2) + "." + ord_pay_sum.Substring(ord_pay_sum.Length - 2);
                    ord_pay_sum = spaces.Remove(8 - ord_pay_sum.Length) + ord_pay_sum;
                    PRE_ORDER_NOPAID_LIST();
                    if (sOUT == "0") goto Pre_order_end_END;
                    all_lines = File.ReadAllText(DB_FILE_TIP());
                    SR = new StreamReader(DB_FILE_TIP());
                    while ((line = SR.ReadLine()) != null)
                    {
                        if (line.Length < 80) continue;
                        // Peržiūrime sOUT sąrašą, ar nėra ord_dok
                        string OUT = sOUT;
                        for (int OUT_nr = OUT.IndexOf(";"); ;)
                        {
                            if (OUT_nr > 0) ord_dok = OUT.Remove(OUT_nr); else ord_dok = OUT;
                            ord_dok = spaces.Remove(8 - ord_dok.Length) + ord_dok;
                            // Ankstesnis sprendimas - pirminę sąsk. spausdinti tik pirmam atsiskaitymo kvite kvite
                            // if (line.Substring(29, 8) == ord_dok && line.Substring(65, 1) == "0")
                            if (line.Substring(29, 8) == ord_dok)   // VERSIJA 2 - pirminiams precekiams spausdinti visus
                            {
                                line_pay = line.Remove(56) + ord_pay_sum + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                all_lines = all_lines.Replace(line, line_pay);
                            }
                            if (OUT_nr < 0) break;
                            OUT = OUT.Substring(OUT_nr + 1);
                            OUT_nr = OUT.IndexOf(";");
                        }
                    }
                    SR.Close();
                    File.WriteAllText(DB_FILE_TIP(), all_lines);
                Pre_order_end_END:
                    sOUT = "0";
                    LOG_END();
                    continue;
                }

                /*********************************************************************************************************************************************/
                /*                                                Neapmokėtų prečekių sumavimas ir sąrašas                                                   */
                /*   Pre_order_x_z.inp - Z-nr                                                                                                                */
                /*   Pre_order_x_z.out - perkelta į viešbutį (0;0) ; anuliuota (kiekis; suma) ; neapmokėta_1 ; neapmokėta_2 ; ...                            */
                /*********************************************************************************************************************************************/
                if (LOG_BEG("x_z"))
                {
                    ord_Z = sINP;
                    long PERKELTA_KIEKIS = 0, PERKELTA_SUMA = 0, ANULIUOTA_KIEKIS = 0, ANULIUOTA_SUMA = 0;
                    sOUT = "0";
                    if (File.Exists(DB_FILE_TIP()))
                    {
                        SR = new StreamReader(DB_FILE_TIP());
                        while ((line = SR.ReadLine()) != null) if (line.Length > 80)
                            if (line.Substring(65, 1) == "0")
                            {
                                ANULIUOTA_KIEKIS++;
                                line = line.Substring(47, 8).Replace(" ", "").Replace(",", "").Replace(".", "");
                                if (line.All(char.IsDigit) == true)
                                    ANULIUOTA_SUMA += Convert.ToInt32(line);
                            }
                        SR.Close();
                        sOUT = PERKELTA_KIEKIS.ToString() + ";" + PERKELTA_SUMA.ToString() + ";" + ANULIUOTA_KIEKIS.ToString() + ";" + ANULIUOTA_SUMA.ToString();
                        // Pridedame sąrašą
                        SR = new StreamReader(DB_FILE_TIP());
                        while ((line = SR.ReadLine()) != null) if (line.Length > 80)
                                if (line.Substring(65, 1) == "0") sOUT += ";" + line.Substring(29, 8).Replace(" ", "");
                        SR.Close();
                    }
                    LOG_END();
                    continue;
                }

                /*********************************************************************************************************************************************/
                /*         ??????????????????????????????????????           PERDAVIMAS Į VIEŠBUTĮ                                                            */
                /*********************************************************************************************************************************************/
                /*
                IEnumerable<IOrder> all_orders = PluginContext.Operations.GetOrders().Where<IOrder>(o => o.Status == OrderStatus.Closed && o.Payments[0].Type.Name == Hotel_Payment_Name);
                List<IOrder> orders = all_orders.ToList<IOrder>();
                foreach (var ord in orders)
                {
                    Order_Info.order_nr = -ord.Number;
                    Order_Info.order_primary_nr = 0;
                    Order_Info.suma = (int)(ord.FullSum * 100);
                    ORDERS_INFO.Add(Order_Info);
                    LOG("Hot:" + Order_Info.order_nr.ToString() + ";" + Order_Info.order_primary_nr.ToString() + ";" + (((double)Order_Info.suma) / 100).ToString("0.00"), 1);
                    Order_Info.order_nr = Order_Info.order_primary_nr = Order_Info.suma = 0;
                }
                */
            }
            /*************************************************************************************************************************************************/
            /* Bendra Pre_order_beg / Pre_order_end  Gauti esamo užsakymo ir neatsiskaitytų susietų sąskaitų sąrašą pagal apmokamą užsakymą (ord_Z + ord_nr) */
            /*************************************************************************************************************************************************/
            void PRE_ORDER_NOPAID_LIST()
            {
                sOUT = "0";
                if (!File.Exists(DB_FILE_TIP())) return;                         // Išankstinių sąskaitų bazės nėra
                ord_nr = spaces.Remove(8 - ord_nr.Length) + ord_nr;
                SR = new StreamReader(DB_FILE_TIP());
                line = null;
                while ((line = SR.ReadLine()) != null)
                {
                    // Ankstesnis sprendimas - pirminę sąsk. spausdinti tik pirmam atsiskaitymo kvite kvite
                    // if (line.Length > 80) if (line.Substring(20, 8) == ord_nr && line.Substring(65, 1) == "0") break;
                    if (line.Length > 80) if (line.Substring(20, 8) == ord_nr) break;
                }
                SR.Close();
                if (line == null) return;                                       // Išankstinė sąskaita nesurasta
                sOUT = (ord_dok = line.Substring(29, 8)).Replace(" ", "");      // Išankstinės sąskaitos ord_dok
                if (line.Substring(38, 8).Replace(" ", "") != "0")
                    sOUT += ";" + line.Substring(38, 8).Replace(" ", "");       // Pridedame pirminės sąskaitos ord_dok
                SR = new StreamReader(DB_FILE_TIP());                           // Peržiūrime sąrašą dar kartą - ar nėra pakartotinai išspausdinų užsakymui sąskaitų
                line = null;
                while ((line = SR.ReadLine()) != null)
                {
                    if (line.Length < 80) continue;
                    if (line.Substring(65, 1) != "0") continue;                 // Jau atsiskaityta
                    if (line.Substring(20, 8) != ord_nr) continue;              // Kitas užsakymas, arba jau atsiskaityta
                    if (line.Substring(29, 8) == ord_dok) continue;             // išankstinė - nekartoti
                    sOUT += ";" + line.Substring(29, 8).Replace(" ", "");       // Pridedame pakartotos sąskaitos ord_dok
                }
                SR.Close();
            }
        }
        public void Dispose()
        {
            //window.Dispatcher.InvokeShutdown();
            //window.Dispatcher.Thread.Join();
        }
    }
}
