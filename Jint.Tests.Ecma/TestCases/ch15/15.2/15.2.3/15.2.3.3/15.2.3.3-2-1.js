/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-1.js
 * @description Object.getOwnPropertyDescriptor returns undefined for undefined property name
 */


function testcase() {
    var o = {};
    var desc = Object.getOwnPropertyDescriptor(o, undefined);
    if (desc === undefined) {
      return true;
    }
 }
runTestCase(testcase);
