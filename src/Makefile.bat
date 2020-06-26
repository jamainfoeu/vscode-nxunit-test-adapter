msbuild /p:Configuration=Release cs\TestRunner.sln
call npm install
call npm run build
call npm run package
