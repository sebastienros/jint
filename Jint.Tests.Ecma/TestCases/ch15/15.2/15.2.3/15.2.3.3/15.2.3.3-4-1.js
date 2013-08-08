/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-1.js
 * @description Object.getOwnPropertyDescriptor returns an object representing a data desc for valid data valued properties
 */


function testcase() {
    var o = {};
    o["foo"] = 101;

    var desc = Object.getOwnPropertyDescriptor(o, "foo");
    if (desc.value === 101 &&
        desc.enumerable === true &&
        desc.writable === true &&
        desc.configurable === true &&
        !desc.hasOwnProperty("get") &&
        !desc.hasOwnProperty("set")) {
      return true;
    }
 }
runTestCase(testcase);
