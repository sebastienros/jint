/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-3.js
 * @description A JSONStringCharacter UnicodeEscape may not include any non=hex characters
 */
function testcase() {
    try {
        JSON.parse('"\\u0X50"') 
       }
     catch (e) {
        return e.name==='SyntaxError'
        }
  }
runTestCase(testcase);
