/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-2.js
 * @description The JSON lexical grammer allows '' as a JSONEscapeCharacter after '' in a JSONString
 */


function testcase() {
    return JSON.parse('"\\\\"')==='\\'; 
  }
runTestCase(testcase);
