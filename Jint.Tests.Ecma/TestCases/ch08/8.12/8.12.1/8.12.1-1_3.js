/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_3.js
 * @description Properties - [[HasOwnProperty]] (old style inherited property)
 */

function testcase() {

    var base = {foo:42};
    var o = Object.create(base);
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
