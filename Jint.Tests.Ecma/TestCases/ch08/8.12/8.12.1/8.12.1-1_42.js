/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_42.js
 * @description Properties - [[HasOwnProperty]] (non-configurable, non-enumerable inherited setter property)
 */

function testcase() {

    var base = {};
    Object.defineProperty(base, "foo", {set: function() {;}});
    var o = Object.create(base);
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
