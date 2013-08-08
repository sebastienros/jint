/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.1/8.12.1-1_40.js
 * @description Properties - [[HasOwnProperty]] (configurable, non-enumerable inherited getter property)
 */

function testcase() {

    var base = {};
    Object.defineProperty(base, "foo", {get: function() {return 42;}, configurable:true});
    var o = Object.create(base);
    return o.hasOwnProperty("foo")===false;

}
runTestCase(testcase);
