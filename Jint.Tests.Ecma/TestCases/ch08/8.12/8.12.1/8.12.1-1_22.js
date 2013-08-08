/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_22.js
 * @description Properties - [[HasOwnProperty]] (literal own getter/setter property)
 */

function testcase() {

    var o = { get foo() { return 42;}, set foo(x) {;} };
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
