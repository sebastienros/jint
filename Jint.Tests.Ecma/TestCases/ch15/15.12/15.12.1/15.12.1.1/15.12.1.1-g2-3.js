/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-3.js
 * @description A JSONString may not be delimited by Uncode escaped quotes 
 */
function testcase() {
    try {
        if (JSON.parse("\\u0022abc\\u0022") ==='abc') return false;
       }
     catch (e) {
        return true;
        }
  }
runTestCase(testcase);
