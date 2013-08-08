/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_43.js
 * @description Properties - [[HasOwnProperty]] (non-configurable, enumerable inherited setter property)
 */

function testcase() {

    var base = {};
    Object.defineProperty(base, "foo", {set: function() {;}, enumerable:true});
    var o = Object.create(base);
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
