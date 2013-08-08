/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-7.js
 * @description Array.prototype.reduce returns initialValue if 'length' is 0 and initialValue is present (subclassed Array, length overridden with obj w/o valueOf (toString))
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  
  var o = { toString: function () { return '0';}};
  f.length = o;
  
  // objects inherit the default valueOf method of the Object object;
  // that simply returns the itself. Since the default valueOf() method
  // does not return a primitive value, ES next tries to convert the object
  // to a number by calling its toString() method and converting the
  // resulting string to a number.

  function cb(){}
  try {
    if(f.reduce(cb,1) === 1)
      return true;
  }
  catch (e) {  }  
 }
runTestCase(testcase);
