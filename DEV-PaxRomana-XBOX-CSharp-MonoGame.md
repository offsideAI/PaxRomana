# PaxRomana XBOX CSharp Monogame 


  Next steps

  Push, then install:

  git add .gitignore .github PaxRomana-XBOX-CSharp-src
  git commit
  git push
  gh run watch

  If the build fails, paste me gh run view --log-failed. Then, from PaxRomana-XBOX-CSharp-src:

  XBOX_IP=<console address> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh

  To try it on the Mac first (needs brew install dotnet):

  cd PaxRomana-XBOX-CSharp-src/desktop
  dotnet run -c Release

