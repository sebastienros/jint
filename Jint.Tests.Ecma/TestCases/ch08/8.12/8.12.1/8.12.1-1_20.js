/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_20.js
 * @description Properties - [[HasOwnProperty]] (literal own getter property)
 */

function testcase() {

    var o = { get foo() { return 42;} };
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
