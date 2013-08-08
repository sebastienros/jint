/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.9/8.12.9-9-b-i_1.js
 * @description Redefine a configurable data property to be an accessor property on a newly non-extensible object
 */


function testcase() {
    var o = {};
    Object.defineProperty(o, "foo", 
                          { value: "hello", 
                            configurable: true});
    Object.preventExtensions(o);
    Object.defineProperty(o, "foo", { get: function() { return 5;} });

    var fooDescrip = Object.getOwnPropertyDescriptor(o, "foo");
    return o.foo===5 && fooDescrip.get!==undefined && fooDescrip.set===undefined && fooDescrip.value===undefined && fooDescrip.configurable===true && fooDescrip.enumerable===false && fooDescrip.writable===undefined;
}
runTestCase(testcase);
