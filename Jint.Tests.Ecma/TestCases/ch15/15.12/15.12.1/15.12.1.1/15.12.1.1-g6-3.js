/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-3.js
 * @description The JSON lexical grammer allows 'b' as a JSONEscapeCharacter after '' in a JSONString
 */


function testcase() {
    return JSON.parse('"\\b"')==='\b'; 
  }
runTestCase(testcase);
