/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.2/11.2.3/11.2.3-3_7.js
 * @description Call arguments are evaluated before the check is made to see if the object is actually callable (getter called as indexed property)
 */


function testcase() {
    var o = { }; 
    Object.defineProperty(o, "bar", {get: function()  {this.barGetter = true; return 42;}, 
                                     set: function(x) {this.barSetter = true; }});
    try {
        o.foo( o["bar"] );
        throw new Exception("o.foo does not exist!");
    } catch(e) {
        return (e instanceof TypeError) && (o.barGetter===true) && (o.barSetter===undefined);
    }
}
runTestCase(testcase);
