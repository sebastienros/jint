/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-1.js
 * @description JSONStrings can be written using double quotes
 */


function testcase() {
  return JSON.parse('"abc"')==="abc"; 
  }
runTestCase(testcase);
