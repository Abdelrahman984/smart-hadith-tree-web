$pptx = "D:\Programming\Full-Stack\Smart-Hadith-Tree\Smart_Hadith_Tree_Presentation.pptx"
$pdf = "D:\Programming\Full-Stack\Smart-Hadith-Tree\Smart_Hadith_Tree_Presentation.pdf"

Write-Host "Opening PowerPoint..."
$pptApp = New-Object -ComObject PowerPoint.Application
try {
    $presentation = $pptApp.Presentations.Open($pptx, [Microsoft.Office.Core.MsoTriState]::msoTrue, [Microsoft.Office.Core.MsoTriState]::msoFalse, [Microsoft.Office.Core.MsoTriState]::msoFalse)
    Write-Host "Exporting to PDF: $pdf"
    # 32 = ppSaveAsPDF
    $presentation.SaveAs($pdf, 32)
    $presentation.Close()
    Write-Host "Done!"
}
catch {
    Write-Error $_
}
finally {
    $pptApp.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($pptApp) | Out-Null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

Get-Item $pdf | Format-List Name, Length
