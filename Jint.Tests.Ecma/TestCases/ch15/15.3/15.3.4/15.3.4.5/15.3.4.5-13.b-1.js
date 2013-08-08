/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-1.js
 * @description Function.prototype.bind, bound fn has a 'length' own property
 */


function testcase() {
  function foo() { }
  var o = {};
  
  var bf = foo.bind(o);
  if (bf.hasOwnProperty('length')) {
    return true;
  }
 }
runTestCase(testcase);
