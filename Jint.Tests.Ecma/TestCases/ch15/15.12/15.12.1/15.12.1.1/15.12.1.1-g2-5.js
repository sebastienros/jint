/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-5.js
 * @description A JSONStrings can contain no JSONStringCharacters (Empty JSONStrings)
 */


function testcase() {
  return JSON.parse('""')===""; 
  }
runTestCase(testcase);
