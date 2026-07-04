"""
Generates a SYNTHETIC Smartbroker+ 'Depotübersicht' PDF fixture for parser tests.
Fake holdings + fake identity — NO real personal or financial data. The column
x-positions and the two-rows-per-position layout mirror a real Smartbroker+ export so
the PdfPig-based SmartbrokerPlusPdfImporter is exercised against a realistic geometry.
Regenerate:  python3 generate_smartbroker_plus_sample.py
"""
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4

W, H = A4  # 595 x 842
OUT = "smartbroker_plus_sample.pdf"

# Right-edge x for each right-aligned numeric column (mirrors the real export).
X_ANZAHL, X_EINSTAND, X_MARKT, X_GV = 291, 363, 417, 489
X_LEFT, X_BOERSE = 115, 510
X_RIGHTVAL = 525  # totals value column

def y(top):  # convert a top-origin coordinate to reportlab's bottom-origin baseline
    return H - top - 8

POSITIONS = [
    # name, isin, qty, cost_price, cost_value, mkt_price, mkt_value, gv_abs, gv_pct, boerse
    ("ACME Robotics Inc.", "US0000000001", "10", "100,00", "1.000,00", "120,50", "1.205,00", "+205,00", "+20,50", "XETRA"),
    ("Beispiel AG",        "DE0000000002", "5",  "50,00",  "250,00",   "44,00",  "220,00",   "-30,00",  "-12,00", "GAT"),
    ("Muster N.V.",        "NL0000000003", "3",  "200,00", "600,00",   "210,00", "630,00",   "+30,00",  "+5,00",  "TQ"),
]

c = canvas.Canvas(OUT, pagesize=A4)
c.setFont("Helvetica", 8)

# Header / identity (synthetic)
c.drawString(X_LEFT, y(109), "Smartbroker AG, Ritterstraße 11, 10969 Berlin")
c.drawString(423, y(110), "Depotnummer:"); c.drawRightString(X_RIGHTVAL, y(111), "0000000001")
c.drawString(424, y(120), "Kontonummer:"); c.drawRightString(X_RIGHTVAL, y(120), "0000000006")
c.drawString(X_LEFT, y(130), "Max Mustermann")
c.drawString(X_LEFT, y(197), "Depotübersicht")

# Export timestamp
c.drawString(X_LEFT, y(241), "Exportzeitpunkt")
c.drawString(X_LEFT, y(260), "Datum");  c.drawRightString(X_RIGHTVAL, y(260), "01.01.2026")
c.drawString(X_LEFT, y(281), "Uhrzeit"); c.drawRightString(X_RIGHTVAL, y(281), "12:00:00")

# Totals
c.drawString(X_LEFT, y(321), "Gesamtdepot"); c.drawRightString(X_RIGHTVAL, y(321), "in EUR")
c.drawString(X_LEFT, y(340), "Depotwert gesamt");      c.drawRightString(X_RIGHTVAL, y(340), "2.055,00 €")
c.drawString(X_LEFT, y(362), "Gewinn relativ gesamt"); c.drawRightString(X_RIGHTVAL, y(361), "+11,08 %")
c.drawString(X_LEFT, y(383), "Gewinn absolut gesamt"); c.drawRightString(X_RIGHTVAL, y(382), "+205,00 €")

# Column headers (two rows)
c.drawString(X_LEFT, y(448), "Assetname"); c.drawString(X_LEFT, y(459), "ISIN/WKN")
c.drawRightString(X_ANZAHL, y(448), "Anzahl"); c.drawRightString(X_ANZAHL, y(459), "Stück")
c.drawRightString(X_EINSTAND, y(448), "Einstandskurs"); c.drawRightString(X_EINSTAND, y(459), "Einstandswert")
c.drawRightString(X_MARKT, y(448), "Marktkurs"); c.drawRightString(X_MARKT, y(459), "Marktwert")
c.drawRightString(X_GV, y(448), "G/V absolut"); c.drawRightString(X_GV, y(459), "G/V prozentual")
c.drawString(X_BOERSE, y(448), "Börse")

# Position rows: name row (top) + isin row (bottom, +11.3), just like the real export.
top = 477
for (name, isin, qty, cp, cv, mp, mv, gva, gvp, boerse) in POSITIONS:
    c.drawString(X_LEFT, y(top), name)
    c.drawRightString(X_ANZAHL, y(top), qty)
    c.drawRightString(X_EINSTAND, y(top), cp + " €")
    c.drawRightString(X_MARKT, y(top), mp + " €")
    c.drawRightString(X_GV, y(top), gva + " €")
    c.drawString(X_BOERSE, y(top), boerse)
    c.drawString(X_LEFT, y(top + 11.3), isin)
    c.drawRightString(X_EINSTAND, y(top + 11.3), cv + " €")
    c.drawRightString(X_MARKT, y(top + 11.3), mv + " €")
    c.drawRightString(X_GV, y(top + 11.3), gvp + " %")
    top += 32

c.showPage(); c.save()
print("wrote", OUT)
