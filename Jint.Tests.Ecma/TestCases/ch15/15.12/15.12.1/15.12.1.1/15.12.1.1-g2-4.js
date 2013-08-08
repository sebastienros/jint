/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-4.js
 * @description A JSONString must both begin and end with double quotes
 */
function testcase() {
    try {
        if (JSON.parse('"ab'+"c'") ==='abc') return false;
       }
     catch (e) {
        return true;
        }
  }
runTestCase(testcase);
