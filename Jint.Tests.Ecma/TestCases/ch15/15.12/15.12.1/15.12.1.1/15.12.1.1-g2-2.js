/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-2.js
 * @description A JSONString may not be delimited by single quotes 
 */
function testcase() {
    try {
        if (JSON.parse("'abc'") ==='abc') return false;
       }
     catch (e) {
        return true;
        }
  }
runTestCase(testcase);
