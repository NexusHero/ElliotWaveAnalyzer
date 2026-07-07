"""
Generates a SYNTHETIC Trade Republic 'Depotübersicht' PDF fixture for parser tests.
Fake holdings + fake identity — NO real personal or financial data. Trade Republic's portfolio
statement lists one holding per row (unlike Smartbroker+'s two-row layout), so the column
x-positions here mirror a single-row-per-position table so the PdfPig-based
TradeRepublicPdfImporter is exercised against a realistic geometry.
Regenerate:  python3 generate_trade_republic_sample.py
"""
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4

W, H = A4  # 595 x 842
OUT = "trade_republic_sample.pdf"

# Right-edge x for each right-aligned numeric column; left-edge for Name/ISIN.
X_NAME, X_ISIN = 40, 185
X_QTY, X_AVG, X_CUR, X_VAL, X_GVABS, X_GVPCT = 285, 335, 385, 440, 490, 545

def y(top):  # convert a top-origin coordinate to reportlab's bottom-origin baseline
    return H - top - 8

POSITIONS = [
    # name, isin, qty, avg_price, cur_price, cur_value, gv_abs, gv_pct
    ("ACME Robotics Inc.", "US0000000001", "10", "100,00", "120,50", "1.205,00", "+205,00", "+20,50"),
    ("Beispiel AG",        "DE0000000002", "5",  "50,00",  "44,00",  "220,00",   "-30,00",  "-12,00"),
    ("Muster N.V.",        "NL0000000003", "3",  "200,00", "210,00", "630,00",   "+30,00",  "+5,00"),
]

c = canvas.Canvas(OUT, pagesize=A4)
c.setFont("Helvetica", 8)

# Header / identity (synthetic)
c.drawString(X_NAME, y(60), "Trade Republic Bank GmbH, Brunnenstraße 19-21, 10119 Berlin")
c.drawString(X_NAME, y(80), "Max Mustermann")
c.drawString(X_NAME, y(100), "IBAN: DE00 0000 0000 0000 0000 06")
c.drawString(X_NAME, y(140), "Depotübersicht")

# Export timestamp
c.drawString(X_NAME, y(170), "Datum"); c.drawRightString(X_GVPCT, y(170), "01.01.2026")
c.drawString(X_NAME, y(184), "Uhrzeit"); c.drawRightString(X_GVPCT, y(184), "12:00:00")

# Totals
c.drawString(X_NAME, y(214), "Depotwert gesamt"); c.drawRightString(X_GVPCT, y(214), "2.055,00 €")
c.drawString(X_NAME, y(228), "Gewinn/Verlust absolut"); c.drawRightString(X_GVPCT, y(228), "+205,00 €")
c.drawString(X_NAME, y(242), "Gewinn/Verlust in %"); c.drawRightString(X_GVPCT, y(242), "+11,08 %")

# Column headers
header_top = 280
c.drawString(X_NAME, y(header_top), "Name")
c.drawString(X_ISIN, y(header_top), "ISIN")
c.drawRightString(X_QTY, y(header_top), "Stück")
c.drawRightString(X_AVG, y(header_top), "Ø Kurs")
c.drawRightString(X_CUR, y(header_top), "Kurs")
c.drawRightString(X_VAL, y(header_top), "Wert")
c.drawRightString(X_GVABS, y(header_top), "G/V")
c.drawRightString(X_GVPCT, y(header_top), "G/V %")

# Position rows: one row per holding.
top = header_top + 20
for (name, isin, qty, avg, cur, val, gva, gvp) in POSITIONS:
    c.drawString(X_NAME, y(top), name)
    c.drawString(X_ISIN, y(top), isin)
    c.drawRightString(X_QTY, y(top), qty)
    c.drawRightString(X_AVG, y(top), avg + " €")
    c.drawRightString(X_CUR, y(top), cur + " €")
    c.drawRightString(X_VAL, y(top), val + " €")
    c.drawRightString(X_GVABS, y(top), gva + " €")
    c.drawRightString(X_GVPCT, y(top), gvp + " %")
    top += 16

c.showPage(); c.save()
print("wrote", OUT)
