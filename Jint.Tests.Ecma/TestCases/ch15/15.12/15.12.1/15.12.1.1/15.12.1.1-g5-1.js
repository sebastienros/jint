/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-1.js
 * @description The JSON lexical grammar allows Unicode escape sequences in a JSONString
 */
function testcase() {
    return JSON.parse('"\\u0058"')==='X'; 
  }
runTestCase(testcase);
