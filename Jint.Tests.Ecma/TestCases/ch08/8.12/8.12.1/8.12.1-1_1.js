/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_1.js
 * @description Properties - [[HasOwnProperty]] (property does not exist)
 */

function testcase() {

    var o = {};
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
