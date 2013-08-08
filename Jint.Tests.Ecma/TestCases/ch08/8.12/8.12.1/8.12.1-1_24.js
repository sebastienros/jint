/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_24.js
 * @description Properties - [[HasOwnProperty]] (literal inherited setter property)
 */

function testcase() {

    var base = { set foo(x) {;} };
    var o = Object.create(base);
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
