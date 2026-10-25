import { describe, expect, it } from 'vitest'
import { interpreteazaOffice } from '@/lib/office-preview'

const rel = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
describe('previzualizarea locală Office', () => {
  it('Word păstrează textul și nu îl interpretează ca HTML', () => {
    const r = interpreteazaOffice({ 'word/document.xml': '<document><p><t>&lt;script&gt;test&lt;/script&gt;</t></p></document>' }, 'docx')
    expect(r[0].linii).toEqual(['<script>test</script>'])
  })
  it('PowerPoint urmează ordinea din prezentare, nu numele fișierului', () => {
    const r = interpreteazaOffice({
      'ppt/presentation.xml': `<presentation xmlns:r="${rel}"><sldIdLst><sldId r:id="b"/><sldId r:id="a"/></sldIdLst></presentation>`,
      'ppt/_rels/presentation.xml.rels': '<Relationships><Relationship Id="a" Target="slides/slide1.xml"/><Relationship Id="b" Target="slides/slide2.xml"/></Relationships>',
      'ppt/slides/slide1.xml': '<sld><p><t>Al doilea</t></p></sld>',
      'ppt/slides/slide2.xml': '<sld><p><t>Primul</t></p></sld>',
    }, 'pptx')
    expect(r.map(x => x.linii[0])).toEqual(['Primul', 'Al doilea'])
  })
  it('Excel păstrează coloanele goale, shared strings și valoarea memorată a formulei', () => {
    const r = interpreteazaOffice({
      'xl/workbook.xml': `<workbook xmlns:r="${rel}"><sheets><sheet name="Buget" r:id="s"/></sheets></workbook>`,
      'xl/_rels/workbook.xml.rels': '<Relationships><Relationship Id="s" Target="worksheets/sheet1.xml"/></Relationships>',
      'xl/sharedStrings.xml': '<sst><si><t>Venit</t></si></sst>',
      'xl/worksheets/sheet1.xml': '<worksheet><row r="3"><c r="A3" t="s"><v>0</v></c><c r="C3"><f>1+1</f><v>2</v></c></row></worksheet>',
    }, 'xlsx')
    expect(r[0].titlu).toBe('Buget')
    expect(r[0].randuri[0]).toEqual({ numar: '3', celule: ['Venit', '', '2'] })
  })
  it('respinge DTD, entități și XML invalid', () => {
    for (const text of ['<!DOCTYPE a><document/>', '<document><p>', '<!ENTITY a "x"><a/>'])
      expect(() => interpreteazaOffice({ 'word/document.xml': text }, 'docx')).toThrow()
  })
})
