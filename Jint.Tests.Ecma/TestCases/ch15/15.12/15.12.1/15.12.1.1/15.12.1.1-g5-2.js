/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-2.js
 * @description A JSONStringCharacter UnicodeEscape may not have fewer than 4 hex characters
 */
function testcase() {
    try {
        JSON.parse('"\\u005"') 
       }
     catch (e) {
        return e.name==='SyntaxError'
        }
  }
runTestCase(testcase);
