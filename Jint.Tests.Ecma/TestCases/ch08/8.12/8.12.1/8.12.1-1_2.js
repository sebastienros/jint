/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_2.js
 * @description Properties - [[HasOwnProperty]] (old style own property)
 */

function testcase() {

    var o = {foo: 42};
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
