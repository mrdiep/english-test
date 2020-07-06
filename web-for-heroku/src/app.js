var express = require('express');
var fs = require('fs');

var path = require('path')
app = express()
app.set('view engine', 'pug')

app.use(express.static( __dirname + '/../assets'));
app.get('/', function (req, res) {

	var films = fs.readdirSync( __dirname + '/../assets').map(file => "<a type='button' class='btn btn-light' href='/" + file + "'>" + file + "</a>").join('<br/><br/>');
	res.render('index', { listFilm: films })


	

  res.end();
})

var port = process.env.PORT || 5000;
app.listen(port);
console.log('server started '+ port);