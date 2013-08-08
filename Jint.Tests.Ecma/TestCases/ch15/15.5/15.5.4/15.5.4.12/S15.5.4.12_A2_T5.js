// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.search (regexp) returns ...
 *
 * @path ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T5.js
 * @description Checking case sensitive of search, argument is RegExp without uppercase symbols
 */

var bString = new String("one two three four five");
var regExp = /four/;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (bString.search(regExp)!== 14) {
  $ERROR('#1: var bString = new String("one two three four five"); var regExp = /four/; bString.search(regExp)=== 14. Actual: '+bString.search(regExp));
}
//
//////////////////////////////////////////////////////////////////////////////

