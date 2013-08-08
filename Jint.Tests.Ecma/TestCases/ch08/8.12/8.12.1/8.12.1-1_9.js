/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_9.js
 * @description Properties - [[HasOwnProperty]] (writable, non-configurable, enumerable own value property)
 */

function testcase() {

    var o = {};
    Object.defineProperty(o, "foo", {value: 42, writable:true, enumerable:true});
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
