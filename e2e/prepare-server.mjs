// Czyści bazę testową tuż przed startem serwera (wykonywane tylko raz, w komendzie webServer)
import { rmSync } from 'node:fs'
rmSync(process.argv[2], { recursive: true, force: true })
