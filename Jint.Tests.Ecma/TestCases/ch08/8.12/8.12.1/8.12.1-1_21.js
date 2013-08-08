/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_21.js
 * @description Properties - [[HasOwnProperty]] (literal own setter property)
 */

function testcase() {

    var o = { set foo(x) {;} };
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
