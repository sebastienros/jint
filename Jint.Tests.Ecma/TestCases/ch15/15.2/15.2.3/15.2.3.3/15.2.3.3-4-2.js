/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-2.js
 * @description Object.getOwnPropertyDescriptor returns undefined for non-existent properties
 */


function testcase() {
    var o = {};

    var desc = Object.getOwnPropertyDescriptor(o, "foo");
    if (desc === undefined) {
      return true;
    }
 }
runTestCase(testcase);
