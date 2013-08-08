/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_27.js
 * @description Properties - [[HasOwnProperty]] (non-configurable, enumerable own getter property)
 */

function testcase() {

    var o = {};
    Object.defineProperty(o, "foo", {get: function() {return 42;}, enumerable:true});
    return o.hasOwnProperty("foo");

}
runTestCase(testcase);
